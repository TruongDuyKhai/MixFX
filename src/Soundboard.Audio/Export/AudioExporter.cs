using NAudio.Wave;
using Soundboard.Audio.Decoding;
using Soundboard.Audio.Effects;
using Soundboard.Audio.Mixing;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Export;

/// <summary>
/// "Export" (§5.4): renders the trimmed, faded, gained sound into a new 48 kHz 16-bit stereo WAV —
/// the format every app and game accepts. Uses the same envelope as playback, so the file sounds
/// exactly like the board.
/// </summary>
public sealed class AudioExporter(ISoundLibrary library) : IAudioExporter
{
    private const int BitsPerSample = 16;
    private const string TempSuffix = ".tmp";

    public Task ExportWavAsync(SoundItem sound, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sound);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        return Task.Run(() => Export(sound, destinationPath, cancellationToken), cancellationToken);
    }

    private void Export(SoundItem sound, string destinationPath, CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(library.GetFilePath(sound));
        var destination = Path.GetFullPath(destinationPath);
        if (string.Equals(Path.GetDirectoryName(destination), Path.GetDirectoryName(source), StringComparison.OrdinalIgnoreCase))
        {
            // The library is the app's own storage; exporting into it could overwrite a sound's only copy.
            throw new InvalidOperationException("Hãy chọn thư mục khác thư mục thư viện của Soundboard.");
        }

        using var decoded = AudioDecoder.Open(source, TimeSpan.FromMilliseconds(Math.Max(0, sound.Edit.TrimStartMs)));
        var frames = AudioDecoder.TrimmedFrames(decoded.SourceDuration, sound.Edit);
        var envelope = EditEnvelope.ForExport(sound.Edit, frames);

        // Callers strip the effects when the SoundEffects flag is off, so the file matches what the board plays.
        var effects = EffectChainFactory.Create(sound.Edit.Effects);

        // Written beside the target first, so a failed export never leaves a truncated file under the chosen name.
        var temp = destination + TempSuffix;
        try
        {
            using (var writer = new WaveFileWriter(temp, new WaveFormat(EngineFormat.SampleRate, BitsPerSample, EngineFormat.Channels)))
            {
                WriteFrames(decoded, writer, envelope, effects, frames, cancellationToken);
            }

            File.Move(temp, destination, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }

    private static void WriteFrames(
        DecodedAudio decoded, WaveFileWriter writer, EditEnvelope envelope, IEffect[] effects, long frames, CancellationToken cancellationToken)
    {
        var buffer = new float[EngineFormat.ScratchSamples];
        long position = 0;
        while (position < frames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var wanted = (int)Math.Min(buffer.Length / EngineFormat.Channels, frames - position) * EngineFormat.Channels;
            var read = decoded.Samples.Read(buffer, 0, wanted);
            read -= read % EngineFormat.Channels;
            foreach (var effect in effects)
            {
                effect.Process(buffer.AsSpan(0, read));
            }

            for (var i = 0; i < read; i += EngineFormat.Channels)
            {
                var gain = envelope.GainAt(position + (i / EngineFormat.Channels));

                // NAudio's 16-bit writer wraps out-of-range values into loud clicks; clip instead.
                buffer[i] = Math.Clamp(buffer[i] * gain, -1f, 1f);
                buffer[i + 1] = Math.Clamp(buffer[i + 1] * gain, -1f, 1f);
            }

            writer.WriteSamples(buffer, 0, read);
            position += read / EngineFormat.Channels;
            if (read < wanted)
            {
                break;
            }
        }
    }
}
