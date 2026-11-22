namespace Soundboard.Audio.Decoding;

/// <summary>A trimmed sound fully decoded to the engine format, shared by every voice that plays it.</summary>
internal sealed class CachedSound(float[] samples)
{
    /// <summary>Interleaved stereo at the engine rate. Never modified after construction.</summary>
    public float[] Samples { get; } = samples;

    public long Frames => Samples.Length / EngineFormat.Channels;
}
