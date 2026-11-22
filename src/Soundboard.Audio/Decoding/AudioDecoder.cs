using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Soundboard.Audio.Conversion;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Decoding;

/// <summary>
/// Opens wav / mp3 / ogg / flac / aac files and converts them to the engine format (PROJECT_PROMPT.md §5.1).
/// Must run on an MTA thread (engine thread or thread pool): Media Foundation readers created on the
/// UI's STA thread fail when read from another thread.
/// </summary>
internal static class AudioDecoder
{
    private const string WavExtension = ".wav";
    private const string OggExtension = ".ogg";

    /// <param name="start">Seek position (the trim start).</param>
    public static DecodedAudio Open(string path, TimeSpan start)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var reader = OpenReader(path);
        try
        {
            if (start > TimeSpan.Zero)
            {
                reader.CurrentTime = start < reader.TotalTime ? start : reader.TotalTime;
            }

            return new DecodedAudio(reader, ToEngineFormat(reader.ToSampleProvider()));
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    /// <summary>Length of the trimmed region in engine frames, clamped to the file.</summary>
    public static long TrimmedFrames(TimeSpan sourceDuration, EditParams edit)
    {
        var sourceFrames = EngineFormat.MsToFrames((long)sourceDuration.TotalMilliseconds);
        var start = EngineFormat.MsToFrames(Math.Max(0, edit.TrimStartMs));
        var end = edit.TrimEndMs is { } trimEnd ? Math.Min(EngineFormat.MsToFrames(trimEnd), sourceFrames) : sourceFrames;
        return Math.Max(0, end - start);
    }

    /// <summary>Decodes up to <paramref name="maxFrames"/> frames into memory.</summary>
    public static CachedSound DecodeAll(DecodedAudio audio, long maxFrames)
    {
        ArgumentNullException.ThrowIfNull(audio);

        var samples = new float[maxFrames * EngineFormat.Channels];
        var filled = 0;
        while (filled < samples.Length)
        {
            var read = audio.Samples.Read(samples, filled, samples.Length - filled);
            filled += read - (read % EngineFormat.Channels);
            if (read == 0)
            {
                break;
            }
        }

        if (filled < samples.Length)
        {
            Array.Resize(ref samples, filled);
        }

        return new CachedSound(samples);
    }

    internal static ISampleProvider ToEngineFormat(ISampleProvider source)
    {
        var converted = source.WaveFormat.Channels switch
        {
            1 => new MonoToStereo(source),
            2 => source,
            _ => new FrontPairSelector(source),
        };

        return converted.WaveFormat.SampleRate == EngineFormat.SampleRate
            ? converted
            : new WdlResamplingSampleProvider(converted, EngineFormat.SampleRate);
    }

    private static WaveStream OpenReader(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(OggExtension, StringComparison.OrdinalIgnoreCase))
        {
            return new VorbisWaveReader(path);
        }

        if (extension.Equals(WavExtension, StringComparison.OrdinalIgnoreCase))
        {
            var wav = new WaveFileReader(path);
            if (wav.WaveFormat.Encoding is WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat)
            {
                return wav;
            }

            // ADPCM, extensible and other wav flavours: Media Foundation decodes them.
            wav.Dispose();
        }

        return new MediaFoundationReader(path, new MediaFoundationReader.MediaFoundationReaderSettings
        {
            RequestFloatOutput = true,
        });
    }
}
