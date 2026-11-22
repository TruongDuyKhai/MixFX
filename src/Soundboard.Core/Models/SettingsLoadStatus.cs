namespace Soundboard.Core.Models;

/// <summary>What <c>ISettingsStore.LoadAsync</c> had to do to produce usable settings.</summary>
public enum SettingsLoadStatus
{
    /// <summary>settings.json was read normally.</summary>
    Loaded,

    /// <summary>No settings file existed yet; defaults were returned and nothing was written.</summary>
    FirstRun,

    /// <summary>settings.json was missing or unreadable; the previous save (settings.json.bak) was used instead.</summary>
    RecoveredFromBackup,

    /// <summary>Neither settings.json nor its backup could be read. The unreadable files were moved aside, not deleted.</summary>
    ResetToDefaults,

    /// <summary>
    /// The file was written by a newer app version. It was read as far as possible, but saving is
    /// blocked because this version would silently drop the fields it does not know.
    /// </summary>
    NewerSchemaReadOnly,
}
