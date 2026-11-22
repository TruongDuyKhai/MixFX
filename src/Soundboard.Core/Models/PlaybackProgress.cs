namespace Soundboard.Core.Models;

/// <summary>Progress of one playing instance of a sound, for the tile progress bars.</summary>
public sealed record PlaybackProgress(Guid SoundId, TimeSpan Position, TimeSpan Duration);
