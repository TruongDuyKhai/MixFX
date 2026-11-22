namespace Soundboard.App.Services;

/// <summary>
/// One app per user session: two instances would install two sets of hooks, fight over the audio
/// devices and overwrite each other's settings. A second launch just brings the first one forward.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    /// <summary>Also referenced by the installer (AppMutex) to detect a running app before upgrading.</summary>
    public const string MutexName = "Soundboard.SingleInstance.6E3F0C51-6F43-4C6B-9C3E-2B7C3F1E9A10";

    private const string ActivateEventName = "Soundboard.Activate.6E3F0C51-6F43-4C6B-9C3E-2B7C3F1E9A10";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>Returns the instance lock, or null after asking the already-running instance to show itself.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        if (createdNew)
        {
            return new SingleInstance(mutex, activate);
        }

        activate.Set();
        activate.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary><paramref name="onActivate"/> runs on a thread-pool thread whenever another launch is attempted.</summary>
    public void ListenForActivation(Action onActivate)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
