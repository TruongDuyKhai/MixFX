using NAudio.Wave;

namespace Soundboard.Audio;

/// <summary>Internal mixing format and the tuning constants of the engine (PROJECT_PROMPT.md §5.1).</summary>
internal static class EngineFormat
{
    public const int SampleRate = 48_000;
    public const int Channels = 2;

    /// <summary>Shared-mode WASAPI buffer. Lower glitches on busy machines; higher adds voice delay.</summary>
    public const int OutputLatencyMs = 30;

    public const int CaptureBufferMs = 20;

    /// <summary>Mic audio buffered before it starts flowing, to absorb jitter between the mic and output clocks.</summary>
    public const int MicTargetLatencyMs = 30;

    /// <summary>Above this the mic buffer is trimmed, so clock drift never turns into growing delay.</summary>
    public const int MicMaxLatencyMs = 100;

    public const int PttFadeMs = 10;

    /// <summary>Fade applied when a sound is stopped early, so it ends without a click.</summary>
    public const int StopFadeMs = 5;

    public const float LimiterCeilingDb = -1f;

    public const float LimiterReleaseMs = 100f;

    /// <summary>Sounds at most this long after trimming are decoded into RAM; longer ones stream (§5.1).</summary>
    public static readonly TimeSpan PreloadMaxDuration = TimeSpan.FromSeconds(15);

    public const int MaxVoicesPerOutput = 64;

    /// <summary>Per-voice disk read-ahead for streamed sounds; filled completely before the sound starts.</summary>
    public const int StreamBufferMs = 2_000;

    /// <summary>Scratch buffers hold one second; callbacks larger than that are processed in chunks.</summary>
    public const int ScratchSamples = SampleRate * Channels;

    public static WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

    public static long MsToFrames(long milliseconds) => milliseconds * SampleRate / 1000;

    public static TimeSpan FramesToTime(long frames) => TimeSpan.FromSeconds((double)frames / SampleRate);
}
