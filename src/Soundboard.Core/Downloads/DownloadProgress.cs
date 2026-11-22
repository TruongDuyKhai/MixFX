namespace Soundboard.Core.Downloads;

/// <summary>Progress of a tool install or an audio download.</summary>
/// <param name="Message">What is happening, ready to show to the user.</param>
/// <param name="Fraction">0…1, or null when the total size is unknown.</param>
public sealed record DownloadProgress(string Message, double? Fraction);
