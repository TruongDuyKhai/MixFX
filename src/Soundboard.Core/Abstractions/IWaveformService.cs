using Soundboard.Core.Models;

namespace Soundboard.Core.Abstractions;

/// <summary>Waveform peaks of library files, cached next to them as <c>{guid}.peaks</c> (§5.4).</summary>
public interface IWaveformService
{
    Task<WaveformData> LoadAsync(SoundItem sound, CancellationToken cancellationToken = default);
}
