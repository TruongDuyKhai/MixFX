namespace Soundboard.Core.Models;

/// <summary>
/// Root of settings.json. Any change to its shape (or to a type it contains) requires bumping
/// <see cref="CurrentSchemaVersion"/> and adding a migration (PROJECT_PROMPT.md §7.4.13).
/// </summary>
public sealed record AppSettings
{
    /// <summary>v2: feature flags, ducking depth, voice changer pitch, playlist and web remote (P6).</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public AudioSettings Audio { get; init; } = new();

    public IReadOnlyList<SoundItem> Sounds { get; init; } = [];

    public IReadOnlyList<Binding> Bindings { get; init; } = [];

    public FeatureFlags Features { get; init; } = new();

    /// <summary>Sound ids in play order (<see cref="FeatureFlags.Playlist"/>). Ids of deleted sounds are skipped.</summary>
    public IReadOnlyList<Guid> Playlist { get; init; } = [];

    public WebRemoteSettings WebRemote { get; init; } = new();
}
