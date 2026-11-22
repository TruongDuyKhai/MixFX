using Soundboard.Core.Models;

namespace Soundboard.Core.Abstractions;

/// <summary>Writes a new WAV file with the sound's edits baked in (§5.4 "Export"). Never modifies the library copy.</summary>
public interface IAudioExporter
{
    Task ExportWavAsync(SoundItem sound, string destinationPath, CancellationToken cancellationToken = default);
}
