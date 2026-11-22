using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.App.ViewModels;

namespace Soundboard.App.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Regression: App.OnExit disposed the view model and then the DI container disposed it again;
        // the second call threw and aborted host disposal, so the engine and hooks were not released cleanly.
        var viewModel = Create();
        viewModel.Start();

        viewModel.Dispose();
        var second = viewModel.Dispose;

        second.Should().NotThrow();
    }

    private static MainViewModel Create() => new(
        new StubEngine(),
        new StubSettings(),
        new StubDispatcher(),
        new StubLibrary(),
        new StubWaveforms(),
        new StubDialogs(),
        new StubStartup(),
        NullLogger<MainViewModel>.Instance);
}
