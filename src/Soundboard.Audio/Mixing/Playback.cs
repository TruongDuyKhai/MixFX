using Soundboard.Core.Models;

namespace Soundboard.Audio.Mixing;

/// <summary>One triggered sound: its voice on the game output and/or on the monitor output.</summary>
internal sealed class Playback(Guid soundId, Voice? gameVoice, Voice? monitorVoice, bool isPreview = false) : IDisposable
{
    public Guid SoundId { get; } = soundId;

    public Voice? GameVoice { get; } = gameVoice;

    public Voice? MonitorVoice { get; } = monitorVoice;

    /// <summary>Editor preview: monitor only, replaced by the next preview.</summary>
    public bool IsPreview { get; } = isPreview;

    public bool IsFinished => (GameVoice?.IsFinished ?? true) && (MonitorVoice?.IsFinished ?? true);

    /// <summary>Played to the end on at least one output (not stopped, not cut by a lost device).</summary>
    public bool Completed => (GameVoice?.EndedNaturally ?? false) || (MonitorVoice?.EndedNaturally ?? false);

    public PlaybackProgress Progress
    {
        get
        {
            var voice = GameVoice is { IsFinished: false } ? GameVoice : MonitorVoice ?? GameVoice!;
            return new PlaybackProgress(SoundId, EngineFormat.FramesToTime(voice.Position), EngineFormat.FramesToTime(voice.TotalFrames));
        }
    }

    public void RequestStop()
    {
        GameVoice?.RequestStop();
        MonitorVoice?.RequestStop();
    }

    /// <summary>Engine thread: reads ahead from disk for streamed voices.</summary>
    public void Pump()
    {
        (GameVoice as StreamedVoice)?.Pump();
        (MonitorVoice as StreamedVoice)?.Pump();
    }

    public void Dispose()
    {
        GameVoice?.Dispose();
        MonitorVoice?.Dispose();
    }
}
