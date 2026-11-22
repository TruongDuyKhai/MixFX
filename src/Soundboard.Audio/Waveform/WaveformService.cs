using Microsoft.Extensions.Logging;
using Soundboard.Audio.Decoding;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Waveform;

/// <summary>
/// Computes per-bucket peaks by decoding the whole file once, then caches them in
/// <c>library\{guid}.peaks</c> so the editor opens instantly next time (§5.4). Library files never
/// change after import, so a cache file never goes stale.
/// </summary>
public sealed class WaveformService(ISoundLibrary library, ILogger<WaveformService> logger) : IWaveformService
{
    /// <summary>10 ms per bucket: fine enough to place trim markers precisely, small enough for minutes of audio.</summary>
    internal const int FramesPerBucket = EngineFormat.SampleRate / 100;

    private const uint CacheMagic = 0x4B504253; // "SBPK"
    private const int CacheVersion = 1;
    private const string TempSuffix = ".tmp";

    public Task<WaveformData> LoadAsync(SoundItem sound, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sound);

        // Thread pool: decoding takes a while, and Media Foundation needs an MTA thread.
        return Task.Run(() => Load(sound, cancellationToken), cancellationToken);
    }

    internal static WaveformData Compute(string audioPath, CancellationToken cancellationToken)
    {
        using var decoded = AudioDecoder.Open(audioPath, TimeSpan.Zero);

        var peaks = new List<float>();
        var buffer = new float[FramesPerBucket * EngineFormat.Channels * 100];
        var bucketPeak = 0f;
        var framesInBucket = 0;
        long totalFrames = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = decoded.Samples.Read(buffer, 0, buffer.Length);
            for (var i = 0; i + 1 < read; i += EngineFormat.Channels)
            {
                bucketPeak = MathF.Max(bucketPeak, MathF.Max(MathF.Abs(buffer[i]), MathF.Abs(buffer[i + 1])));
                if (++framesInBucket == FramesPerBucket)
                {
                    peaks.Add(bucketPeak);
                    bucketPeak = 0f;
                    framesInBucket = 0;
                }
            }

            totalFrames += read / EngineFormat.Channels;
            if (read < buffer.Length)
            {
                break;
            }
        }

        if (framesInBucket > 0)
        {
            peaks.Add(bucketPeak);
        }

        return new WaveformData(EngineFormat.FramesToTime(totalFrames), EngineFormat.FramesToTime(FramesPerBucket), [.. peaks]);
    }

    internal static void WriteCache(string path, WaveformData data)
    {
        var temp = path + TempSuffix;
        using (var writer = new BinaryWriter(File.Create(temp)))
        {
            writer.Write(CacheMagic);
            writer.Write(CacheVersion);
            writer.Write(data.Duration.Ticks);
            writer.Write(data.BucketDuration.Ticks);
            writer.Write(data.Peaks.Count);
            foreach (var peak in data.Peaks)
            {
                writer.Write(peak);
            }
        }

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Returns null for a missing, truncated or foreign file; the caller then recomputes.</summary>
    internal static WaveformData? ReadCache(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadUInt32() != CacheMagic || reader.ReadInt32() != CacheVersion)
            {
                return null;
            }

            var duration = TimeSpan.FromTicks(reader.ReadInt64());
            var bucket = TimeSpan.FromTicks(reader.ReadInt64());
            var count = reader.ReadInt32();
            if (count < 0 || bucket <= TimeSpan.Zero || (long)count * sizeof(float) != reader.BaseStream.Length - reader.BaseStream.Position)
            {
                return null;
            }

            var peaks = new float[count];
            for (var i = 0; i < count; i++)
            {
                peaks[i] = reader.ReadSingle();
            }

            return new WaveformData(duration, bucket, peaks);
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }

    private WaveformData Load(SoundItem sound, CancellationToken cancellationToken)
    {
        var cachePath = library.GetPeaksPath(sound.LibraryFile);
        try
        {
            if (ReadCache(cachePath) is { } cached)
            {
                return cached;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Waveform cache {Path} unreadable; recomputing", cachePath);
        }

        var data = Compute(library.GetFilePath(sound), cancellationToken);
        try
        {
            WriteCache(cachePath, data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The cache only saves time; the editor works without it.
            logger.LogWarning(ex, "Could not write waveform cache {Path}", cachePath);
        }

        return data;
    }
}
