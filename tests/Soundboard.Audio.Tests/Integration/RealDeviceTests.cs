using Microsoft.Extensions.Logging.Abstractions;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Soundboard.Audio.Dsp;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Tests.Integration;

/// <summary>
/// Runs the real engine against a real render device and records that device's loopback to prove
/// sound actually reaches it. Explicit because it needs hardware:
/// <c>dotnet test tests/Soundboard.Audio.Tests -- xUnit.Explicit=only</c>.
/// Target device: env var SOUNDBOARD_TEST_RENDER_DEVICE (name prefix), default "CABLE Input" (VB-Cable),
/// which is exactly the path the game hears. Pick a silent device (e.g. an unused digital output)
/// to run it without audible sound.
/// </summary>
public sealed class RealDeviceTests : IDisposable
{
    private const string DeviceVariable = "SOUNDBOARD_TEST_RENDER_DEVICE";
    private const string DefaultDevice = "CABLE Input";

    private readonly string _dir = Directory.CreateTempSubdirectory("soundboard-device-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact(Explicit = true)]
    public async Task GameOutput_DeliversSoundToDevice_AndLimiterHoldsCeiling()
    {
        var device = FindRenderDevice(Environment.GetEnvironmentVariable(DeviceVariable) ?? DefaultDevice);
        Assert.SkipWhen(device is null, "Render device not found; install VB-Cable or set " + DeviceVariable);

        // A virtual cable is inaudible, so it gets a +6 dBFS tone that the limiter must tame.
        // A physical device only gets a -50 dBFS tone: enough to measure, too quiet to startle anyone.
        var isCable = device!.Value.Name.StartsWith(DefaultDevice, StringComparison.OrdinalIgnoreCase);
        var amplitude = isCable ? 2f : QuietAmplitude;

        var file = Path.Combine(_dir, "tone.wav");
        using (var writer = new WaveFileWriter(file, WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2)))
        {
            var tone = Signals.Sine(48_000 * 2, 2, amplitude, frequency: 1_000);
            writer.WriteSamples(tone, 0, tone.Length);
        }

        var sound = new SoundItem(Guid.NewGuid(), "tone", "tone.wav", "#FFFFFF", 1f, PlaybackKind.Preloaded, EditParams.None);
        using var engine = new AudioEngine(new FixedLibrary(file), NullLogger<AudioEngine>.Instance);
        engine.ApplySettings(new AudioSettings
        {
            GameOutputDeviceId = device.Value.Id,

            // Unknown ids leave the mic and monitor closed, so only the game path is measured.
            MonitorOutputDeviceId = MissingDeviceId,
            MicDeviceId = MissingDeviceId,
        });

        await WaitUntilAsync(() => engine.GetStatus(AudioEndpointRole.GameOutput) == AudioEndpointStatus.Running);
        engine.GetStatus(AudioEndpointRole.MonitorOutput).Should().Be(AudioEndpointStatus.NotConfigured);

        using var loopback = new LoopbackRecorder(device.Value.Id);
        engine.Play(sound);
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        var captured = loopback.Stop();

        engine.IsPlaying(sound.Id).Should().BeTrue();
        if (isCable)
        {
            Signals.Peak(captured).Should().BeGreaterThan(0.5f, "the tone must reach the cable");

            // Small headroom for the device's own resampler; still clearly below 0 dBFS.
            Signals.Peak(captured).Should().BeLessThan(Decibels.ToGain(-1f) + 0.03f, "Output A is limited to -1 dBFS");
        }
        else
        {
            Signals.Peak(captured).Should().BeInRange(QuietAmplitude * 0.5f, QuietAmplitude * 1.5f, "the tone must reach the device");
        }
    }

    [Fact(Explicit = true)]
    public async Task Microphone_ReachesGameOutputOnlyWhileGateIsOpen()
    {
        var device = FindRenderDevice(Environment.GetEnvironmentVariable(DeviceVariable) ?? DefaultDevice);
        Assert.SkipWhen(device is null, "Render device not found; install VB-Cable or set " + DeviceVariable);

        using var engine = new AudioEngine(new FixedLibrary(string.Empty), NullLogger<AudioEngine>.Instance);
        var settings = new AudioSettings
        {
            GameOutputDeviceId = device!.Value.Id,
            MonitorOutputDeviceId = MissingDeviceId,
            MicMode = MicMode.PushToTalk,
        };
        engine.ApplySettings(settings);
        await WaitUntilAsync(() => engine.GetStatus(AudioEndpointRole.GameOutput) == AudioEndpointStatus.Running
            && engine.GetStatus(AudioEndpointRole.Microphone) == AudioEndpointStatus.Running);

        var closed = await RecordAsync(device.Value.Id, TimeSpan.FromSeconds(1));
        engine.IsVoiceTransmitting.Should().BeFalse();

        engine.SetPushToTalk(true);
        var open = await RecordAsync(device.Value.Id, TimeSpan.FromSeconds(1));
        engine.IsVoiceTransmitting.Should().BeTrue();
        engine.SetPushToTalk(false);

        Signals.Peak(closed).Should().Be(0f, "PTT not held: no voice may reach the game");
        Signals.Peak(open).Should().BeGreaterThan(0f, "PTT held: the mic (even just room noise) must reach the game");
    }

    private static async Task<float[]> RecordAsync(string deviceId, TimeSpan duration)
    {
        using var loopback = new LoopbackRecorder(deviceId);
        await Task.Delay(duration, TestContext.Current.CancellationToken);
        return loopback.Stop();
    }

    private const float QuietAmplitude = 0.003f;
    private const string MissingDeviceId = "{00000000-0000-0000-0000-000000000000}";

    private static (string Id, string Name)? FindRenderDevice(string prefix)
    {
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                if (device.FriendlyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return (device.ID, device.FriendlyName);
                }
            }
        }

        return null;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        condition().Should().BeTrue();
    }

    private sealed class FixedLibrary(string path) : ISoundLibrary
    {
        public string GetFilePath(SoundItem sound) => path;
    }

    private sealed class LoopbackRecorder : IDisposable
    {
        private readonly MMDevice _device;
        private readonly WasapiLoopbackCapture _capture;
        private readonly List<float> _samples = [];
        private readonly Lock _lock = new();

        public LoopbackRecorder(string deviceId)
        {
            using var enumerator = new MMDeviceEnumerator();
            _device = enumerator.GetDevice(deviceId);
            _capture = new WasapiLoopbackCapture(_device);
            _capture.DataAvailable += (_, e) =>
            {
                var floats = new float[e.BytesRecorded / 4];
                Buffer.BlockCopy(e.Buffer, 0, floats, 0, e.BytesRecorded);
                lock (_lock)
                {
                    _samples.AddRange(floats);
                }
            };
            _capture.StartRecording();
        }

        public float[] Stop()
        {
            _capture.StopRecording();
            lock (_lock)
            {
                return [.. _samples];
            }
        }

        public void Dispose()
        {
            _capture.Dispose();
            _device.Dispose();
        }
    }
}
