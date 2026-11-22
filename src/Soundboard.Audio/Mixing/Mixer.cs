using System.Collections.Concurrent;
using NAudio.Wave;

namespace Soundboard.Audio.Mixing;

/// <summary>
/// Sums the voices playing on one output. New voices arrive through a lock-free queue; the voice
/// array is owned by the audio thread, so <see cref="Read"/> never locks or allocates (§7.3.8).
/// Always returns a full buffer (silence when idle) so the device keeps running.
/// </summary>
internal sealed class Mixer : ISampleProvider
{
    private readonly ConcurrentQueue<Voice> _pending = new();
    private readonly Voice?[] _voices = new Voice?[EngineFormat.MaxVoicesPerOutput];
    private readonly float[] _scratch = new float[EngineFormat.ScratchSamples];

    // Audio thread only.
    private int _voiceCount;

    public WaveFormat WaveFormat => EngineFormat.WaveFormat;

    /// <summary>Any thread: queues a voice; it starts on the next callback.</summary>
    public void Add(Voice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        _pending.Enqueue(voice);
    }

    /// <summary>
    /// Ends every voice. Only call while no audio thread is reading this mixer (device stopped),
    /// because it touches state the audio thread owns.
    /// </summary>
    public void Reset()
    {
        while (_pending.TryDequeue(out var voice))
        {
            voice.MarkFinished();
        }

        for (var i = 0; i < _voiceCount; i++)
        {
            _voices[i]!.MarkFinished();
            _voices[i] = null;
        }

        _voiceCount = 0;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var output = buffer.AsSpan(offset, count);
        output.Clear();

        while (_pending.TryDequeue(out var voice))
        {
            if (_voiceCount < _voices.Length)
            {
                _voices[_voiceCount++] = voice;
            }
            else
            {
                // Over the polyphony limit: drop the newest rather than allocate a bigger array here.
                voice.MarkFinished();
            }
        }

        var i = 0;
        while (i < _voiceCount)
        {
            var voice = _voices[i]!;
            MixVoice(voice, output);

            if (voice.IsFinished)
            {
                _voiceCount--;
                _voices[i] = _voices[_voiceCount];
                _voices[_voiceCount] = null;
            }
            else
            {
                i++;
            }
        }

        return count;
    }

    private void MixVoice(Voice voice, Span<float> output)
    {
        var done = 0;
        while (done < output.Length)
        {
            var chunk = Math.Min(output.Length - done, _scratch.Length);
            var read = voice.Read(_scratch.AsSpan(0, chunk));

            var target = output.Slice(done, read);
            var source = _scratch.AsSpan(0, read);
            for (var s = 0; s < read; s++)
            {
                target[s] += source[s];
            }

            done += read;
            if (read < chunk || voice.IsFinished)
            {
                return;
            }
        }
    }
}
