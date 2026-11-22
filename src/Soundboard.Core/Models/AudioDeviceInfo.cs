namespace Soundboard.Core.Models;

/// <summary>An active audio endpoint as shown in device pickers.</summary>
/// <param name="Id">Endpoint id; stable across reboots, unlike <paramref name="Name"/>.</param>
/// <param name="Name">Display name, e.g. "CABLE Input (VB-Audio Virtual Cable)".</param>
/// <param name="Flow">Capture or render.</param>
/// <param name="IsDefault">Whether Windows currently uses it as the default device.</param>
/// <param name="IsVirtualCable">Whether it belongs to VB-Audio Virtual Cable.</param>
public sealed record AudioDeviceInfo(string Id, string Name, AudioDeviceFlow Flow, bool IsDefault, bool IsVirtualCable);
