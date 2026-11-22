namespace Soundboard.Core.Models;

/// <summary>Ranges and rules of the non-destructive editor (PROJECT_PROMPT.md §5.4).</summary>
public static class EditLimits
{
    public const float MinGainDb = -24f;
    public const float MaxGainDb = 12f;

    /// <summary>Normalize brings the trimmed peak to this level, leaving the limiter nothing to do.</summary>
    public const float NormalizeTargetDb = -1f;

    /// <summary>Shortest region the trim markers may leave, so a sound can never be trimmed to nothing by accident.</summary>
    public const int MinTrimmedMs = 50;

    /// <summary>Sounds at most this long after trimming are kept in RAM; longer ones stream from disk (§5.1).</summary>
    public static readonly TimeSpan PreloadMaxDuration = TimeSpan.FromSeconds(15);

    public static PlaybackKind KindFor(TimeSpan trimmedDuration) =>
        trimmedDuration <= PreloadMaxDuration ? PlaybackKind.Preloaded : PlaybackKind.Streamed;

    /// <summary>Gain that brings <paramref name="peak"/> (linear, 0…1+) to <see cref="NormalizeTargetDb"/>, within the editor's range.</summary>
    public static float NormalizeGainDb(float peak)
    {
        if (peak <= 0f || float.IsNaN(peak))
        {
            // Silence cannot be normalised; boosting it would only raise noise.
            return 0f;
        }

        var gain = NormalizeTargetDb - (20f * MathF.Log10(peak));
        return Math.Clamp(gain, MinGainDb, MaxGainDb);
    }
}
