using Soundboard.Core.Abstractions;
using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.Core.Tests.Fakes;

internal sealed class FakeSettingsProvider(AppSettings initial) : ISettingsProvider
{
    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public AppSettings Current { get; private set; } = initial;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var previous = Current;
        Current = change(previous);
        Changed?.Invoke(this, new SettingsChangedEventArgs(previous, Current));
    }
}
