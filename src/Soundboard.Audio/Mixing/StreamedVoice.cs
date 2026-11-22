using Soundboard.Audio.Buffers;
using Soundboard.Audio.Decoding;

namespace Soundboard.Audio.Mixing;

/// <summary>
/// Plays a long sound from disk. The engine thread decodes ahead into a lock-free ring
/// (<see cref="Pump"/>); the audio thread only copies from that ring, so it never touches the disk (§7.3.8).
/// </summary>
internal sealed class StreamedVoice : Voice
{
    private const int Channels = EngineFormat.Channels;

    private readonly DecodedAudio _decoder;
    private readonly SpscRingBuffer _ring;

    // Engine thread only.
    private readonly float[] _pumpBuffer;
    private long _framesDecoded;

    // Set by the engine thread after its last write, read by the audio thread.
    private int _decoderEnded;

    public StreamedVoice(Guid soundId, DecodedAudio decoder, long totalFrames, EditEnvelope envelope)
        : base(soundId, totalFrames, envelope)
    {
        ArgumentNullException.ThrowIfNull(decoder);

        _decoder = decoder;
        _ring = new SpscRingBuffer((int)EngineFormat.MsToFrames(EngineFormat.StreamBufferMs) * Channels);
        _pumpBuffer = new float[EngineFormat.ScratchSamples / 4];
    }

    /// <summary>Engine thread: tops the read-ahead buffer up from disk.</summary>
    public void Pump()
    {
        if (Volatile.Read(ref _decoderEnded) != 0 || IsFinished)
        {
            return;
        }

        var ended = false;
        var free = _ring.Capacity - _ring.Count;
        while (true)
        {
            var framesLeft = TotalFrames - _framesDecoded;
            if (framesLeft <= 0)
            {
                ended = true;
                break;
            }

            var samplesWanted = (int)Math.Min(Math.Min(free, _pumpBuffer.Length), framesLeft * Channels);
            samplesWanted -= samplesWanted % Channels;
            if (samplesWanted <= 0)
            {
                break;
            }

            var read = _decoder.Samples.Read(_pumpBuffer, 0, samplesWanted);
            read -= read % Channels;
            _ring.Write(_pumpBuffer.AsSpan(0, read));
            _framesDecoded += read / Channels;
            free -= read;

            // NAudio readers only return short at the end of the file.
            if (read < samplesWanted)
            {
                ended = true;
                break;
            }
        }

        if (ended)
        {
            // Published after the last write so the audio thread never reports the end while data is still queued.
            Volatile.Write(ref _decoderEnded, 1);
        }
    }

    protected override int ReadFrames(Span<float> destination, out bool sourceEnded)
    {
        var decoderEnded = Volatile.Read(ref _decoderEnded) != 0;
        var read = _ring.Read(destination);
        sourceEnded = decoderEnded && _ring.Count == 0;
        return read / Channels;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _decoder.Dispose();
        }

        base.Dispose(disposing);
    }
}
