using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Devices;

/// <summary>Lists endpoints and resolves which device each engine role should use.</summary>
internal static class DeviceCatalog
{
    /// <summary>Render endpoint VB-Cable installs; the game listens to its "CABLE Output" twin.</summary>
    public const string CableInputPrefix = "CABLE Input";

    public const string CableOutputPrefix = "CABLE Output";

    private const string VirtualCableMarker = "VB-Audio Virtual Cable";

    public static IReadOnlyList<AudioDeviceInfo> List(MMDeviceEnumerator enumerator, AudioDeviceFlow flow)
    {
        var dataFlow = ToDataFlow(flow);
        var defaultId = DefaultId(enumerator, dataFlow, Role.Multimedia);
        var result = new List<AudioDeviceInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active))
        {
            using (device)
            {
                var name = device.FriendlyName;
                result.Add(new AudioDeviceInfo(device.ID, name, flow, device.ID == defaultId, IsVirtualCable(name)));
            }
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return result;
    }

    public static bool IsVirtualCable(string name) =>
        name.Contains(VirtualCableMarker, StringComparison.OrdinalIgnoreCase)
        || name.StartsWith(CableInputPrefix, StringComparison.OrdinalIgnoreCase)
        || name.StartsWith(CableOutputPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The device to open for <paramref name="role"/>, or null when there is none. The caller owns the result.
    /// With nothing configured, the game output only ever falls back to VB-Cable, never to speakers:
    /// sending the mic to speakers would echo the user's voice back at them.
    /// </summary>
    public static MMDevice? Resolve(MMDeviceEnumerator enumerator, AudioEndpointRole role, string? configuredId)
    {
        if (configuredId is not null)
        {
            return ActiveById(enumerator, configuredId);
        }

        return role switch
        {
            AudioEndpointRole.Microphone => Default(enumerator, DataFlow.Capture, Role.Console),
            AudioEndpointRole.MonitorOutput => Default(enumerator, DataFlow.Render, Role.Multimedia),
            AudioEndpointRole.GameOutput => FindCableInput(enumerator),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    private static MMDevice? ActiveById(MMDeviceEnumerator enumerator, string id)
    {
        MMDevice device;
        try
        {
            device = enumerator.GetDevice(id);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            // Unknown or malformed id: device uninstalled since it was chosen, or settings edited by hand.
            return null;
        }

        if (device.State == DeviceState.Active)
        {
            return device;
        }

        device.Dispose();
        return null;
    }

    private static MMDevice? Default(MMDeviceEnumerator enumerator, DataFlow flow, Role role) =>
        enumerator.HasDefaultAudioEndpoint(flow, role) ? enumerator.GetDefaultAudioEndpoint(flow, role) : null;

    private static string? DefaultId(MMDeviceEnumerator enumerator, DataFlow flow, Role role)
    {
        using var device = Default(enumerator, flow, role);
        return device?.ID;
    }

    private static MMDevice? FindCableInput(MMDeviceEnumerator enumerator)
    {
        MMDevice? found = null;
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            if (found is null && device.FriendlyName.StartsWith(CableInputPrefix, StringComparison.OrdinalIgnoreCase))
            {
                found = device;
            }
            else
            {
                device.Dispose();
            }
        }

        return found;
    }

    private static DataFlow ToDataFlow(AudioDeviceFlow flow) => flow == AudioDeviceFlow.Capture ? DataFlow.Capture : DataFlow.Render;
}
