namespace Soundboard.Core.Models;

/// <summary>
/// Root of settings.json. Any change to its shape (or to a type it contains) requires bumping
/// <see cref="CurrentSchemaVersion"/> and adding a migration (PROJECT_PROMPT.md §7.4.13).
/// </summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public AudioSettings Audio { get; init; } = new();

    public IReadOnlyList<SoundItem> Sounds { get; init; } = [];

    public IReadOnlyList<Binding> Bindings { get; init; } = [];

    public FeatureFlags Features { get; init; } = new();
}
