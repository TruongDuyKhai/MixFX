using System.Text.Json.Nodes;
using Soundboard.Core.Models;

namespace Soundboard.Storage.Migrations;

/// <summary>Runs the chain of <see cref="ISettingsMigration"/> steps from a file's version up to the current one.</summary>
internal sealed class SettingsMigrator
{
    private const int FirstSchemaVersion = 1;

    private readonly Dictionary<int, ISettingsMigration> _byFromVersion;

    public SettingsMigrator(IEnumerable<ISettingsMigration> migrations, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetVersion, FirstSchemaVersion);

        TargetVersion = targetVersion;
        _byFromVersion = [];
        foreach (var migration in migrations)
        {
            if (migration.FromVersion < FirstSchemaVersion || migration.FromVersion >= targetVersion)
            {
                throw new ArgumentException(
                    $"{migration.GetType().Name} upgrades from v{migration.FromVersion}, outside v{FirstSchemaVersion}..v{targetVersion - 1}.",
                    nameof(migrations));
            }

            if (!_byFromVersion.TryAdd(migration.FromVersion, migration))
            {
                throw new ArgumentException($"Two migrations upgrade from v{migration.FromVersion}.", nameof(migrations));
            }
        }

        // Checked here, at startup, so bumping the schema version without writing its migration
        // fails every test run instead of failing on a user's machine.
        for (var version = FirstSchemaVersion; version < targetVersion; version++)
        {
            if (!_byFromVersion.ContainsKey(version))
            {
                throw new ArgumentException($"No migration upgrades from v{version}.", nameof(migrations));
            }
        }
    }

    public int TargetVersion { get; }

    /// <summary>The production chain. Register each new <c>MigrateV{n}ToV{n+1}</c> here.</summary>
    public static SettingsMigrator CreateDefault() => new([], AppSettings.CurrentSchemaVersion);

    /// <summary>Upgrades <paramref name="root"/> in place from <paramref name="fromVersion"/> to <see cref="TargetVersion"/>.</summary>
    public void Migrate(JsonObject root, int fromVersion)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfLessThan(fromVersion, FirstSchemaVersion);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fromVersion, TargetVersion);

        for (var version = fromVersion; version < TargetVersion; version++)
        {
            _byFromVersion[version].Apply(root);
            root[SettingsJson.SchemaVersionProperty] = version + 1;
        }
    }
}
