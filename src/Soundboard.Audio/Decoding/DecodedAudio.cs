using NAudio.Wave;

namespace Soundboard.Audio.Decoding;

/// <summary>An open audio file exposed in the engine format. Owns the file handle.</summary>
internal sealed class DecodedAudio(WaveStream reader, ISampleProvider samples) : IDisposable
{
    /// <summary>48 kHz float stereo, starting at the position the file was opened at.</summary>
    public ISampleProvider Samples { get; } = samples;

    /// <summary>Length of the whole file as reported by its decoder.</summary>
    public TimeSpan SourceDuration => reader.TotalTime;

    public void Dispose() => reader.Dispose();
}
