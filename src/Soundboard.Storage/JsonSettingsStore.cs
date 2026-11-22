using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;
using Soundboard.Storage.Migrations;

namespace Soundboard.Storage;

/// <summary>
/// settings.json with atomic writes, a one-generation backup and recovery that never destroys
/// the user's data (PROJECT_PROMPT.md §5.7, §7.4).
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private const string PreservedSuffixPrefix = ".corrupt-";
    private const string PreservedTimestampFormat = "yyyyMMdd'T'HHmmssfff'Z'";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly StoragePaths _paths;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly SettingsMigrator _migrator = SettingsMigrator.CreateDefault();

    // One operation at a time: two saves interleaving their write/rename steps could leave the
    // backup pointing at the wrong generation.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Guarded by _gate.
    private bool _saveBlocked;

    public JsonSettingsStore(StoragePaths paths, TimeProvider timeProvider, ILogger<JsonSettingsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private enum ReadOutcome
    {
        Ok,
        Missing,
        Unreadable,
        NewerSchema,
    }

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _saveBlocked = false;
            var preserved = new List<string>();

            var main = await ReadAsync(_paths.SettingsFile, cancellationToken).ConfigureAwait(false);
            switch (main.Outcome)
            {
                case ReadOutcome.Ok:
                    return new SettingsLoadResult(main.Settings, SettingsLoadStatus.Loaded, preserved);
                case ReadOutcome.NewerSchema:
                    _saveBlocked = true;
                    return new SettingsLoadResult(main.Settings, SettingsLoadStatus.NewerSchemaReadOnly, preserved);
                case ReadOutcome.Unreadable:
                    preserved.Add(MoveAside(_paths.SettingsFile));
                    break;
            }

            var backup = await ReadAsync(_paths.SettingsBackupFile, cancellationToken).ConfigureAwait(false);
            switch (backup.Outcome)
            {
                case ReadOutcome.Ok:
                    _logger.LogWarning("Settings restored from backup {BackupPath}", _paths.SettingsBackupFile);
                    return new SettingsLoadResult(backup.Settings, SettingsLoadStatus.RecoveredFromBackup, preserved);
                case ReadOutcome.NewerSchema:
                    _saveBlocked = true;
                    return new SettingsLoadResult(backup.Settings, SettingsLoadStatus.NewerSchemaReadOnly, preserved);
                case ReadOutcome.Unreadable:
                    preserved.Add(MoveAside(_paths.SettingsBackupFile));
                    break;
            }

            if (main.Outcome == ReadOutcome.Missing && backup.Outcome == ReadOutcome.Missing)
            {
                return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.FirstRun, preserved);
            }

            _logger.LogError(
                "No readable settings found; starting from defaults. Unreadable files kept at {PreservedFiles}",
                preserved);
            return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.ResetToDefaults, preserved);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_saveBlocked)
            {
                throw new InvalidOperationException(
                    "settings.json was written by a newer version of the app. Saving would drop settings this version does not know, so it is blocked.");
            }

            Directory.CreateDirectory(_paths.RootDirectory);
            await WriteTempFileAsync(settings with { SchemaVersion = AppSettings.CurrentSchemaVersion }, cancellationToken)
                .ConfigureAwait(false);

            if (File.Exists(_paths.SettingsFile))
            {
                // Single OS-level swap: the old file becomes the backup and the new one takes its
                // place, so a crash at any point leaves a complete settings file on disk.
                File.Replace(_paths.SettingsTempFile, _paths.SettingsFile, _paths.SettingsBackupFile, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(_paths.SettingsTempFile, _paths.SettingsFile);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task WriteTempFileAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var stream = new FileStream(_paths.SettingsTempFile, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        });
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SettingsJson.SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            // Without this the rename can reach the disk before the data does, and a power cut
            // would leave an empty settings.json.
            stream.Flush(flushToDisk: true);
        }
    }

    private async Task<ReadAttempt> ReadAsync(string path, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return ReadAttempt.Missing;
        }
        catch (DirectoryNotFoundException)
        {
            return ReadAttempt.Missing;
        }

        // Any other I/O error (locked file, access denied) propagates on purpose: the content may be
        // fine, so treating it as corrupt and recovering could overwrite good data on the next save.
        JsonObject root;
        int version;
        try
        {
            root = ParseRoot(bytes);
            version = ReadSchemaVersion(root);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Settings file {Path} is unreadable", path);
            return ReadAttempt.Unreadable;
        }

        if (version > _migrator.TargetVersion)
        {
            _logger.LogWarning(
                "Settings file {Path} has schema v{FileVersion}, newer than this app's v{AppVersion}; opening read-only",
                path, version, _migrator.TargetVersion);
            return new ReadAttempt(ReadOutcome.NewerSchema, TryDeserializeNewer(root, path) ?? new AppSettings());
        }

        try
        {
            _migrator.Migrate(root, version);
            var settings = root.Deserialize<AppSettings>(SettingsJson.SerializerOptions)
                ?? throw new JsonException("Settings deserialized to null.");
            return new ReadAttempt(ReadOutcome.Ok, settings);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Settings file {Path} is unreadable", path);
            return ReadAttempt.Unreadable;
        }
    }

    private AppSettings? TryDeserializeNewer(JsonObject root, string path)
    {
        try
        {
            return root.Deserialize<AppSettings>(SettingsJson.SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Newer settings file {Path} could not be read by this version; using defaults without saving", path);
            return null;
        }
    }

    private static JsonObject ParseRoot(byte[] bytes)
    {
        // Notepad and other editors may add a BOM; the UTF-8 span parser does not skip it.
        ReadOnlySpan<byte> json = bytes;
        if (json.StartsWith(Utf8Bom))
        {
            json = json[Utf8Bom.Length..];
        }

        return JsonNode.Parse(json, documentOptions: SettingsJson.DocumentOptions) as JsonObject
            ?? throw new JsonException("Settings root is not a JSON object.");
    }

    private static int ReadSchemaVersion(JsonObject root)
    {
        if (root.TryGetPropertyValue(SettingsJson.SchemaVersionProperty, out var node)
            && node is JsonValue value
            && value.TryGetValue<int>(out var version)
            && version >= 1)
        {
            return version;
        }

        throw new JsonException($"Missing or invalid '{SettingsJson.SchemaVersionProperty}'.");
    }

    /// <summary>Renames an unreadable file instead of deleting it, so the user (or we) can still rescue its content.</summary>
    private string MoveAside(string path)
    {
        var stamp = _timeProvider.GetUtcNow().ToString(PreservedTimestampFormat, CultureInfo.InvariantCulture);
        var preservedPath = path + PreservedSuffixPrefix + stamp;

        // overwrite: false — never clobber a copy preserved earlier.
        File.Move(path, preservedPath, overwrite: false);
        _logger.LogWarning("Moved unreadable settings file {Path} to {PreservedPath}", path, preservedPath);
        return preservedPath;
    }

    private readonly record struct ReadAttempt(ReadOutcome Outcome, AppSettings Settings)
    {
        public static ReadAttempt Missing { get; } = new(ReadOutcome.Missing, new AppSettings());

        public static ReadAttempt Unreadable { get; } = new(ReadOutcome.Unreadable, new AppSettings());
    }
}
