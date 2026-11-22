namespace Soundboard.Core.Abstractions;

/// <summary>
/// One stage of an effect chain (PROJECT_PROMPT.md §5.4 "Effect chain", P6). Works in place on
/// interleaved 48 kHz stereo float. <see cref="Process"/> runs on the audio thread, so
/// implementations must not allocate, lock, log or do I/O (§7.3.8); each playing voice owns its own instance.
/// </summary>
public interface IEffect
{
    /// <summary>The <see cref="Models.EffectConfig.EffectId"/> this effect implements.</summary>
    string Id { get; }

    void Process(Span<float> interleavedStereo);

    /// <summary>Clears internal state (delay lines, tails).</summary>
    void Reset();
}
