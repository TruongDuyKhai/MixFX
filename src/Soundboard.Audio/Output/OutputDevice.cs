using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Soundboard.Audio.Conversion;

namespace Soundboard.Audio.Output;

/// <summary>
/// Plays an engine-format source on one WASAPI device in shared mode, adapting rate and channel
/// count to the device's mix format ourselves so Windows never inserts its own converter.
/// Owns the <see cref="MMDevice"/> it was given.
/// </summary>
internal sealed class OutputDevice : IDisposable
{
    private const int BitsPerFloat = 32;

    private readonly MMDevice _device;
    private readonly WasapiOut _output;
    private readonly Action<OutputDevice, Exception> _onLost;

    private int _disposed;

    public OutputDevice(MMDevice device, ISampleProvider source, Action<OutputDevice, Exception> onLost)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onLost);

        _device = device;
        _onLost = onLost;
        DeviceId = device.ID;
        DeviceName = device.FriendlyName;

        WaveFormat mixFormat;
        using (var client = device.AudioClient)
        {
            mixFormat = client.MixFormat;
        }

        var chain = source;
        if (mixFormat.SampleRate != EngineFormat.SampleRate)
        {
            chain = new WdlResamplingSampleProvider(chain, mixFormat.SampleRate);
        }

        if (mixFormat.Channels != EngineFormat.Channels)
        {
            chain = new ChannelAdapter(chain, mixFormat.Channels);
        }

        var deviceFormat = mixFormat.BitsPerSample == BitsPerFloat && IsFloat(mixFormat)
            ? mixFormat
            : WaveFormat.CreateIeeeFloatWaveFormat(mixFormat.SampleRate, mixFormat.Channels);
        var wave = new FloatToWaveProvider(chain, deviceFormat);
        Prewarm(wave);

        _output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, EngineFormat.OutputLatencyMs);
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(wave);
    }

    public string DeviceId { get; }

    public string DeviceName { get; }

    public void Start() => _output.Play();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _output.PlaybackStopped -= OnPlaybackStopped;
        _output.Stop();
        _output.Dispose();
        _device.Dispose();
    }

    private static bool IsFloat(WaveFormat format) =>
        format.Encoding == WaveFormatEncoding.IeeeFloat
        || (format is WaveFormatExtensible && Capture.SampleConversion.GetKind(format) == Capture.CaptureSampleKind.Float32);

    /// <summary>
    /// Runs one second through the chain before the device starts, so resamplers size their internal
    /// buffers now instead of allocating inside the first real-time callbacks (§7.3.8).
    /// </summary>
    private static void Prewarm(IWaveProvider wave)
    {
        var oneSecond = new byte[wave.WaveFormat.AverageBytesPerSecond];
        wave.Read(oneSecond, 0, oneSecond.Length - (oneSecond.Length % wave.WaveFormat.BlockAlign));
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null && Volatile.Read(ref _disposed) == 0)
        {
            _onLost(this, e.Exception);
        }
    }
}
