using Soundboard.Audio.Dsp;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Mixing;

/// <summary>Volume, gain and fade curve of one playing sound, evaluated per frame at playback time (§5.4).</summary>
internal readonly struct EditEnvelope
{
    private readonly float _gain;
    private readonly long _fadeInFrames;
    private readonly long _fadeOutFrames;
    private readonly long _totalFrames;

    public EditEnvelope(float gain, long fadeInFrames, long fadeOutFrames, long totalFrames)
    {
        _gain = gain;
        _fadeInFrames = Math.Max(0, fadeInFrames);
        _fadeOutFrames = Math.Max(0, fadeOutFrames);
        _totalFrames = totalFrames;
    }

    public static EditEnvelope For(SoundItem sound, long totalFrames) => new(
        sound.Volume * Decibels.ToGain(sound.Edit.GainDb),
        EngineFormat.MsToFrames(sound.Edit.FadeInMs),
        EngineFormat.MsToFrames(sound.Edit.FadeOutMs),
        totalFrames);

    /// <summary>Edits only (trim/fade/gain), without the per-sound mixing volume: what "Export" bakes into the file.</summary>
    public static EditEnvelope ForExport(EditParams edit, long totalFrames) => new(
        Decibels.ToGain(edit.GainDb),
        EngineFormat.MsToFrames(edit.FadeInMs),
        EngineFormat.MsToFrames(edit.FadeOutMs),
        totalFrames);

    public float GainAt(long frame)
    {
        var gain = _gain;
        if (frame < _fadeInFrames)
        {
            gain *= (float)frame / _fadeInFrames;
        }

        var framesToEnd = _totalFrames - frame;
        if (framesToEnd < _fadeOutFrames)
        {
            gain *= (float)Math.Max(0, framesToEnd) / _fadeOutFrames;
        }

        return gain;
    }
}
