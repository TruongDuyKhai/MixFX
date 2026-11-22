using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Soundboard.Audio.Devices;

/// <summary>
/// Windows calls these on its own threads and expects a fast return, so each callback only asks
/// the engine to re-check its devices later.
/// </summary>
internal sealed class DeviceNotificationClient(Action onDevicesChanged) : IMMNotificationClient
{
    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => onDevicesChanged();

    public void OnDeviceAdded(string pwstrDeviceId) => onDevicesChanged();

    public void OnDeviceRemoved(string deviceId) => onDevicesChanged();

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => onDevicesChanged();

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        // Volume and name changes do not affect which device we use.
    }
}
