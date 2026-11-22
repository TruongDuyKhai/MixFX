using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Soundboard.Audio.Buffers;
using Soundboard.Audio.Conversion;

namespace Soundboard.Audio.Capture;

/// <summary>
/// Captures the real microphone into a lock-free ring and exposes it to the game output in the
/// engine format. Owns the <see cref="MMDevice"/> it was given.
/// </summary>
internal sealed class MicInput : IDisposable
{
    private readonly MMDevice _device;
    private readonly WasapiCapture _capture;
    private readonly WaveFormat _format;
    private readonly CaptureSampleKind _kind;
    private readonly SpscRingBuffer _ring;
    private readonly float[] _scratch;
    private readonly Action<MicInput, Exception> _onLost;

    private int _disposed;

    public MicInput(MMDevice device, Action<MicInput, Exception> onLost)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(onLost);

        _device = device;
        _onLost = onLost;
        DeviceId = device.ID;
        DeviceName = device.FriendlyName;

        _capture = new WasapiCapture(device, useEventSync: true, EngineFormat.CaptureBufferMs);
        _format = _capture.WaveFormat;
        _kind = SampleConversion.GetKind(_format);

        // One second of mono at the device rate: far more than the latency cap, so writes never fail in practice.
        _ring = new SpscRingBuffer(_format.SampleRate);
        _scratch = new float[_format.SampleRate / 4];

        ISampleProvider chain = new MicRingSampleProvider(_ring, _format.SampleRate);
        if (_format.SampleRate != EngineFormat.SampleRate)
        {
            chain = new WdlResamplingSampleProvider(chain, EngineFormat.SampleRate);
        }

        Output = new MonoToStereo(chain);

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
    }

    public string DeviceId { get; }

    public string DeviceName { get; }

    /// <summary>Mic in the engine format, read by the game output's audio thread.</summary>
    public ISampleProvider Output { get; }

    public void Start() => _capture.StartRecording();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _device.Dispose();
    }

    // Capture thread: same rules as an audio callback, no allocation or blocking.
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var bytes = e.Buffer.AsSpan(0, e.BytesRecorded);
        while (bytes.Length >= _format.BlockAlign)
        {
            var frames = Math.Min(bytes.Length / _format.BlockAlign, _scratch.Length);
            var byteCount = frames * _format.BlockAlign;
            SampleConversion.ToMono(bytes[..byteCount], _format, _kind, _scratch);

            // If the ring is full the newest audio is dropped; the reader trims latency anyway.
            _ring.Write(_scratch.AsSpan(0, frames));
            bytes = bytes[byteCount..];
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null && Volatile.Read(ref _disposed) == 0)
        {
            _onLost(this, e.Exception);
        }
    }
}
