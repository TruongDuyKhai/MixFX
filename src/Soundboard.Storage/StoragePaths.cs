namespace Soundboard.Storage;

/// <summary>Every on-disk location the app uses, in one place (PROJECT_PROMPT.md §7.5.17).</summary>
public sealed class StoragePaths
{
    public const string AppFolderName = "Soundboard";

    private const string SettingsFileName = "settings.json";
    private const string BackupSuffix = ".bak";
    private const string TempSuffix = ".tmp";
    private const string LogsFolderName = "logs";
    private const string LibraryFolderName = "library";
    private const string ToolsFolderName = "tools";
    private const string DownloadsFolderName = "downloads";

    /// <param name="rootDirectory">Folder holding settings, logs and the library. Tests pass a temp folder.</param>
    public StoragePaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public string SettingsFile => Path.Combine(RootDirectory, SettingsFileName);

    public string SettingsBackupFile => SettingsFile + BackupSuffix;

    public string SettingsTempFile => SettingsFile + TempSuffix;

    public string LogsDirectory => Path.Combine(RootDirectory, LogsFolderName);

    /// <summary>The app's own copies of imported sounds (§5.3).</summary>
    public string LibraryDirectory => Path.Combine(RootDirectory, LibraryFolderName);

    /// <summary>External tools the app downloads itself (yt-dlp, Deno; ADR 0005). Inside the data folder so "Xóa cả dữ liệu" removes them.</summary>
    public string ToolsDirectory => Path.Combine(RootDirectory, ToolsFolderName);

    /// <summary>Scratch space for downloads until they are copied into the library.</summary>
    public string DownloadsDirectory => Path.Combine(RootDirectory, DownloadsFolderName);

    /// <summary><c>%AppData%\Soundboard</c> for the current Windows user.</summary>
    public static StoragePaths ForCurrentUser()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            // Falling back to a relative path would scatter settings next to whatever the working directory is.
            throw new InvalidOperationException("The current user has no AppData folder.");
        }

        return new StoragePaths(Path.Combine(appData, AppFolderName));
    }
}
