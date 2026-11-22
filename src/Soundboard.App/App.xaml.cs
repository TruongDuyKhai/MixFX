using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Soundboard.App.Services;
using Soundboard.App.ViewModels;
using Soundboard.Audio;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Actions;
using Soundboard.Core.Models;
using Soundboard.Storage;

namespace Soundboard.App;

/// <summary>Composition root: the only place that creates implementations (PROJECT_PROMPT.md §4.1).</summary>
public partial class App : Application
{
    private const int StartupFailedExitCode = 1;

    // Serilog inserts the date before the extension: soundboard-20261122.log.
    private const string LogFileName = "soundboard-.log";
    private const int RetainedLogFileCount = 14;
    private const long MaxLogFileBytes = 10 * 1024 * 1024;

    private static readonly TimeSpan HostStopTimeout = TimeSpan.FromSeconds(5);

    private IHost? _host;

    // async void is forced by the WPF override; everything runs inside try so no exception escapes unlogged.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var paths = StoragePaths.ForCurrentUser();
            Log.Logger = CreateLogger(paths);
            RegisterUnhandledExceptionLogging();
            Log.Information("Soundboard {Version} starting, data folder {DataFolder}", AppInfo.Version, paths.RootDirectory);

            _host = BuildHost(paths);
            await _host.StartAsync();

            var settings = _host.Services.GetRequiredService<SettingsService>();
            var loadResult = await settings.LoadAsync();

            StartAudio(_host.Services, settings.Current);

            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            viewModel.ApplySettingsLoadResult(loadResult);

            var window = _host.Services.GetRequiredService<Views.MainWindow>();
            MainWindow = window;
            window.Show();

            if (viewModel.SettingsWarning is { } warning)
            {
                MessageBox.Show(window, warning, "Soundboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            MessageBox.Show(
                $"Soundboard không khởi động được:\n{ex.Message}\n\nChi tiết nằm trong file log.",
                "Soundboard", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(StartupFailedExitCode);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                SaveSettingsOnExit(_host.Services.GetRequiredService<SettingsService>());

                // Task.Run: blocking the UI thread on an await that wants to resume on it would deadlock.
                Task.Run(() => _host.StopAsync(HostStopTimeout)).GetAwaiter().GetResult();
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Clean shutdown failed");
        }
        finally
        {
            Log.Information("Soundboard exiting with code {ExitCode}", e.ApplicationExitCode);
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private static void StartAudio(IServiceProvider services, AppSettings settings)
    {
        var engine = services.GetRequiredService<IAudioEngine>();
        engine.ApplySettings(settings.Audio);
        _ = PreloadSoundsAsync(engine, settings.Sounds);
    }

    private static async Task PreloadSoundsAsync(IAudioEngine engine, IReadOnlyList<SoundItem> sounds)
    {
        try
        {
            await engine.PreloadAsync(sounds).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Preloading is an optimisation: sounds still decode on first play.
            Log.Warning(ex, "Preloading sounds failed");
        }
    }

    private static void SaveSettingsOnExit(SettingsService settings)
    {
        if (!settings.CanSave)
        {
            Log.Warning("Settings not saved on exit: they were never loaded or belong to a newer app version");
            return;
        }

        Task.Run(() => settings.SaveAsync()).GetAwaiter().GetResult();
    }

    private static IHost BuildHost(StoragePaths paths)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // No appsettings.json / environment / console logging: config lives in settings.json.
            DisableDefaults = true,
            ContentRootPath = AppContext.BaseDirectory,
        });

        // DisableDefaults also turns validation off; keep it so a missing registration fails at startup.
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }));

        var services = builder.Services;
        services.AddSerilog();

        // The host still installs ConsoleLifetime; its "Press Ctrl+C" banner is meaningless in a GUI app's log.
        services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);

        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ISoundLibrary, SoundLibrary>();
        services.AddSingleton<IAudioEngine, AudioEngine>();
        services.AddSingleton<IActionDispatcher, ActionDispatcher>();

        services.AddSingleton<SettingsService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<Views.MainWindow>();

        return builder.Build();
    }

    private static Serilog.Core.Logger CreateLogger(StoragePaths paths) =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, LogFileName),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedLogFileCount,
                fileSizeLimitBytes: MaxLogFileBytes,
                rollOnFileSizeLimit: true)
            .CreateLogger();

    private void RegisterUnhandledExceptionLogging()
    {
        // Log only: the default crash behaviour is kept so a broken state is never silently continued.
        DispatcherUnhandledException += (_, args) =>
            Log.Fatal(args.Exception, "Unhandled exception on the UI thread");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception; process terminating");
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
            Log.Error(args.Exception, "Unobserved task exception");
    }
}
