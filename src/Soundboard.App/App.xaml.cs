using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Soundboard.App.Services;
using Soundboard.App.ViewModels;
using Soundboard.App.Views;
using Soundboard.Audio;
using Soundboard.Audio.Export;
using Soundboard.Audio.Waveform;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Actions;
using Soundboard.App.WebRemote;
using Soundboard.App.YouTube;
using Soundboard.Core.Models;
using Soundboard.Core.Playlist;
using Soundboard.Input;
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

    private SingleInstance? _singleInstance;
    private IHost? _host;
    private TrayIcon? _tray;

    // async void is forced by the WPF override; everything runs inside try so no exception escapes unlogged.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = SingleInstance.TryAcquire();
        if (_singleInstance is null)
        {
            // Another instance is already running and has been asked to show itself.
            Shutdown();
            return;
        }

        try
        {
            var paths = StoragePaths.ForCurrentUser();
            Log.Logger = CreateLogger(paths);
            RegisterUnhandledExceptionLogging();
            Log.Information("Soundboard {Version} starting, data folder {DataFolder}", AppInfo.Version, paths.RootDirectory);

            _host = BuildHost(paths);
            await _host.StartAsync();
            var services = _host.Services;

            var settings = services.GetRequiredService<SettingsService>();
            var loadResult = await settings.LoadAsync();

            services.GetRequiredService<SettingsSync>().Start();
            StartHotkeys(services.GetRequiredService<IInputService>());

            var viewModel = services.GetRequiredService<MainViewModel>();
            viewModel.ApplySettingsLoadResult(loadResult);
            viewModel.Start();

            var window = services.GetRequiredService<MainWindow>();
            MainWindow = window;
            _tray = new TrayIcon(window.ShowFromTray, viewModel.StopAll, Shutdown);
            _singleInstance.ListenForActivation(() => Dispatcher.BeginInvoke(window.ShowFromTray));

            var startHidden = e.Args.Contains(StartupRegistration.MinimizedArgument, StringComparer.OrdinalIgnoreCase);
            if (!startHidden)
            {
                window.Show();
            }

            if (viewModel.SettingsWarning is { } warning)
            {
                window.ShowFromTray();
                MessageBox.Show(window, warning, "Soundboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (loadResult.Status == SettingsLoadStatus.FirstRun && !startHidden)
            {
                services.GetRequiredService<IDialogService>().ShowSetup();
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
            _tray?.Dispose();
            if (_host is not null)
            {
                SaveSettingsOnExit(_host.Services.GetRequiredService<SettingsService>());

                // Disposing the host disposes every singleton it created (view model, engine, hooks):
                // WASAPI stops and hooks are removed (§7.3.11).
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
            _singleInstance?.Dispose();
            Log.Information("Soundboard exiting with code {ExitCode}", e.ApplicationExitCode);
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private static void StartHotkeys(IInputService input)
    {
        try
        {
            input.Start();
        }
        catch (Exception ex)
        {
            // The board still works by clicking; only global hotkeys are lost.
            Log.Error(ex, "Could not install keyboard/mouse hooks");
            MessageBox.Show(
                $"Không cài được phím tắt toàn cục, chỉ dùng được bằng cách bấm chuột:\n{ex.Message}",
                "Soundboard", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        services.AddSingleton<IWaveformService, WaveformService>();
        services.AddSingleton<IAudioExporter, AudioExporter>();
        services.AddSingleton<IInputService, InputService>();
        services.AddSingleton<IActionDispatcher, ActionDispatcher>();

        // Every trigger source reaches these through IActionDispatcher (§4.2).
        services.AddSingleton<IAction, PlaySoundAction>();
        services.AddSingleton<IAction, StopAllAction>();
        services.AddSingleton<IAction, ToggleMicAction>();
        services.AddSingleton<IAction, PttHoldAction>();
        services.AddSingleton<IAction>(sp => VolumeStepAction.Up(sp.GetRequiredService<ISettingsProvider>()));
        services.AddSingleton<IAction>(sp => VolumeStepAction.Down(sp.GetRequiredService<ISettingsProvider>()));

        // P6, each behind a FeatureFlags switch that defaults to off.
        services.AddSingleton<PlaylistController>();
        services.AddSingleton<IAction>(sp => PlaylistAction.Next(sp.GetRequiredService<PlaylistController>()));
        services.AddSingleton<IAction>(sp => PlaylistAction.Stop(sp.GetRequiredService<PlaylistController>()));
        services.AddSingleton<WebRemoteRequestHandler>();
        services.AddSingleton<WebRemoteServer>();
        services.AddSingleton(sp => new DownloadTools(
            sp.GetRequiredService<StoragePaths>(),
            DownloadTools.CreateHttpClient(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DownloadTools>>()));
        services.AddSingleton<IAudioDownloader, YtDlpDownloader>();

        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsProvider>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<SettingsSync>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IStartupRegistration, StartupRegistration>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<GlobalHotkeysViewModel>();
        services.AddTransient<SetupViewModel>();

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
