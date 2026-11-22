using System.Buffers.Binary;
using NAudio.Wave;

namespace Soundboard.Audio.Capture;

/// <summary>Sample formats a WASAPI capture device can deliver in shared mode.</summary>
internal enum CaptureSampleKind
{
    Float32,
    Pcm16,
    Pcm24,
    Pcm32,
}

/// <summary>Converts raw capture bytes to mono float without allocating (runs on the capture thread).</summary>
internal static class SampleConversion
{
    private const float Pcm16Scale = 1f / 32_768f;
    private const float Pcm24Scale = 1f / 8_388_608f;
    private const float Pcm32Scale = 1f / 2_147_483_648f;

    // KSDATAFORMAT_SUBTYPE_PCM / KSDATAFORMAT_SUBTYPE_IEEE_FLOAT
    private static readonly Guid PcmSubtype = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid FloatSubtype = new("00000003-0000-0010-8000-00aa00389b71");

    public static CaptureSampleKind GetKind(WaveFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
            || (format is WaveFormatExtensible extensible && extensible.SubFormat == FloatSubtype);
        var isPcm = format.Encoding == WaveFormatEncoding.Pcm
            || (format is WaveFormatExtensible pcm && pcm.SubFormat == PcmSubtype);

        return (isFloat, isPcm, format.BitsPerSample) switch
        {
            (true, _, 32) => CaptureSampleKind.Float32,
            (_, true, 16) => CaptureSampleKind.Pcm16,
            (_, true, 24) => CaptureSampleKind.Pcm24,
            (_, true, 32) => CaptureSampleKind.Pcm32,
            _ => throw new NotSupportedException($"Unsupported capture format: {format}."),
        };
    }

    /// <summary>Averages all channels of each frame in <paramref name="source"/> into <paramref name="destination"/>.</summary>
    /// <returns>Number of frames written.</returns>
    public static int ToMono(ReadOnlySpan<byte> source, WaveFormat format, CaptureSampleKind kind, Span<float> destination)
    {
        var channels = format.Channels;
        var bytesPerSample = format.BitsPerSample / 8;
        var frames = Math.Min(source.Length / format.BlockAlign, destination.Length);

        for (var frame = 0; frame < frames; frame++)
        {
            var sum = 0f;
            var frameBytes = source.Slice(frame * format.BlockAlign, format.BlockAlign);
            for (var c = 0; c < channels; c++)
            {
                sum += ReadSample(frameBytes.Slice(c * bytesPerSample, bytesPerSample), kind);
            }

            destination[frame] = sum / channels;
        }

        return frames;
    }

    private static float ReadSample(ReadOnlySpan<byte> bytes, CaptureSampleKind kind) => kind switch
    {
        CaptureSampleKind.Float32 => BinaryPrimitives.ReadSingleLittleEndian(bytes),
        CaptureSampleKind.Pcm16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) * Pcm16Scale,
        CaptureSampleKind.Pcm24 => (bytes[0] | (bytes[1] << 8) | ((sbyte)bytes[2] << 16)) * Pcm24Scale,
        CaptureSampleKind.Pcm32 => BinaryPrimitives.ReadInt32LittleEndian(bytes) * Pcm32Scale,
        _ => 0f,
    };
}
