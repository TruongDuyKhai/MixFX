using System.Text.Json.Nodes;

namespace Soundboard.Storage.Migrations;

/// <summary>
/// Upgrades raw settings JSON by exactly one schema version. Name implementations
/// <c>MigrateV{n}ToV{n+1}</c> and test each with a fixture from the old version (PROJECT_PROMPT.md §7.4.13).
/// </summary>
internal interface ISettingsMigration
{
    /// <summary>Schema version this migration reads. It produces <c>FromVersion + 1</c>.</summary>
    int FromVersion { get; }

    /// <summary>
    /// Edits <paramref name="root"/> in place. Works on raw JSON rather than the C# models because the
    /// old shape no longer exists as a type. Throw <see cref="System.Text.Json.JsonException"/> when the
    /// input does not have the expected shape, so the file is treated as unreadable instead of half-migrated.
    /// The migrator updates <c>schemaVersion</c>; implementations must not.
    /// </summary>
    void Apply(JsonObject root);
}
