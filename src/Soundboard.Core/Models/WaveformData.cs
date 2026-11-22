namespace Soundboard.Core.Models;

/// <summary>
/// Peak envelope of a whole library file for the editor (§5.4): <see cref="Peaks"/>[i] is the largest
/// absolute sample in the i-th block of <see cref="BucketDuration"/>.
/// </summary>
public sealed class WaveformData(TimeSpan duration, TimeSpan bucketDuration, float[] peaks)
{
    public TimeSpan Duration { get; } = duration;

    public TimeSpan BucketDuration { get; } = bucketDuration;

    public IReadOnlyList<float> Peaks { get; } = peaks;

    /// <summary>Largest peak between two positions; used by Normalize on the trimmed region.</summary>
    public float PeakBetween(TimeSpan start, TimeSpan end)
    {
        if (Peaks.Count == 0 || BucketDuration <= TimeSpan.Zero)
        {
            return 0f;
        }

        var first = Math.Clamp((int)(start / BucketDuration), 0, Peaks.Count - 1);
        var last = Math.Clamp((int)Math.Ceiling(end / BucketDuration) - 1, first, Peaks.Count - 1);
        var peak = 0f;
        for (var i = first; i <= last; i++)
        {
            peak = MathF.Max(peak, Peaks[i]);
        }

        return peak;
    }
}
