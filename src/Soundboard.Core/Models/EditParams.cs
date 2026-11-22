namespace Soundboard.Core.Models;

/// <summary>
/// Non-destructive edits applied at playback time. The imported file itself is never modified
/// (PROJECT_PROMPT.md §5.4).
/// </summary>
public sealed record EditParams(
    int TrimStartMs,
    int? TrimEndMs,
    int FadeInMs,
    int FadeOutMs,
    float GainDb,
    IReadOnlyList<EffectConfig> Effects)
{
    /// <summary>No trim, no fade, no gain, no effects.</summary>
    public static EditParams None { get; } = new(0, null, 0, 0, 0f, []);
}
