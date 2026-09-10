using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Logging;
using ENRLLauncher.Core.Services;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.View;
using ENRLLauncher.MVVM.ViewModel;

namespace ENRLLauncher;

public partial class App
{
    private SplashWindow? _splash;
    private IAppLogger? _logger;

    public static IServiceProvider Services { get; private set; } = null!;

    private void ConfigureServices(IServiceCollection services)
    {
        // Storage
        services.AddSingleton<IJsonStorageService, JsonStorageService>();

        // Logging
        services.AddSingleton<IAppLogger>(_ => _logger ?? new FileLogger(Globals.g_LogsDir));

        // Core Services
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<IUpdaterService, UpdaterService>();
        services.AddSingleton<VersionCheckerUI>();
        services.AddSingleton<ILayoutService, LayoutService>();
        services.AddSingleton<ILauncherService, LauncherService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();

        // ViewModels
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        // Views
        services.AddSingleton<MainWindow>(sp => new MainWindow(sp.GetRequiredService<MainWindowViewModel>()));
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Display Splash Screen
        _splash = new SplashWindow();
        _splash.SourceInitialized += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            _splash.Left = area.Left + (area.Width - _splash.Width) / 2;
            _splash.Top = area.Top + (area.Height - _splash.Height) / 2;
        };
        _splash.Show();
        _splash.UpdateStatus("Starting…");

        var sw = Stopwatch.StartNew();
        void Mark(string label)
        {
            var ms = sw.ElapsedMilliseconds;
            _logger?.Write(AppLogLevel.Debug, "Startup", $"{label} @ {ms} ms");
            _splash?.UpdateStatus(label + "…");
        }

        // 2. Storage Directory Check
        if (!StorageBootstrapper.TryEnsureCoreDirs(out var dirError))
        {
            try { _splash?.Close(); _splash = null; } catch { /* ignore */ }

            MessageBox.Show(
                $"Critical Error initializing local storage folders:\n\n{dirError}\n\nThe application will now close.",
                "Storage Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
            return;
        }

        // Initialize file logger as soon as storage directories are verified
        _logger = new FileLogger(Globals.g_LogsDir);
        _logger.Write(AppLogLevel.Info, "Startup", $"App starting v{Globals.g_AppVersion}");
        Mark("Storage folders verified");

        // 3. Configure Dependency Injection & Services
        Mark("Loading services");
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        // Non-blocking cleanup of previous update temp folders
        _ = Services.GetRequiredService<IUpdaterService>().CleanupOldUpdatesAsync();

        // 4. Update Check on Splash Screen
        Mark("Checking for updates");
        _logger.Write(AppLogLevel.Info, "Startup", "Initiating update check on splash screen");
        try
        {
            var updateUi = Services.GetRequiredService<VersionCheckerUI>();
            var updateTask = updateUi.EnforceRequiredAsync();
            var firstChance = await Task.WhenAny(updateTask, Task.Delay(TimeSpan.FromSeconds(3)));

            if (firstChance == updateTask)
            {
                await updateTask;
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
                Mark("Update check complete");
                _logger.Write(AppLogLevel.Info, "Startup", "Splash update check completed successfully");
            }
            else
            {
                _logger.Write(AppLogLevel.Warning, "Startup", "Splash update check exceeded 3s timeout; continuing startup");
            }
        }
        catch (Exception ex)
        {
            _logger.Write(AppLogLevel.Warning, "Startup", $"Splash update check failed: {ex.Message}");
        }

        // Brief delay to ensure smooth splash presentation
        await Task.Delay(400);

        // 5. Initialize Main Window
        Mark("Loading application");
        var mainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        // 6. Dismiss splash upon first window render and check optional updates
        mainWindow.ContentRendered += (_, _) =>
        {
            Mark("First window rendered");
            try
            {
                _splash?.Close();
                _splash = null;
            }
            catch { /* ignore */ }

            // Background non-blocking check to prompt for newer available versions
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    _logger.Write(AppLogLevel.Info, "UpdateCheck", "Triggering post-render optional update check");
                    var updateUi = Services.GetRequiredService<VersionCheckerUI>();
                    await updateUi.CheckAsync(showUpToDatePopup: false, owner: mainWindow);
                }
                catch (Exception ex)
                {
                    _logger.Write(AppLogLevel.Warning, "UpdateCheck", $"Background update check failed: {ex.Message}");
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        };

        mainWindow.Show();
        Mark("Window shown");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Write(AppLogLevel.Info, "Shutdown", "App shutting down");

        if (_logger is FileLogger fl)
        {
            fl.Dispose();
        }

        if (Services.GetService<IHttpService>() is IDisposable http)
        {
            http.Dispose();
        }

        base.OnExit(e);
    }
}
