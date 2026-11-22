namespace Soundboard.App.ViewModels;

/// <summary>One entry of a device picker. <see cref="Id"/> null means "Windows default" (or "auto" for the game output).</summary>
public sealed record DeviceOption(string? Id, string Name);
