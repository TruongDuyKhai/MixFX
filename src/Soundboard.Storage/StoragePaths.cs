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
