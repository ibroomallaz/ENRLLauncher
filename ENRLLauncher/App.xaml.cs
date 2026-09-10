using System;
using System.Diagnostics;
using System.Threading.Tasks;
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

    public App()
    {
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Storage
        services.AddSingleton<IJsonStorageService, JsonStorageService>();

        // Logging
        services.AddSingleton<IAppLogger>(_ => new FileLogger(Globals.g_LogsDir));

        // Core Services
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<IUpdaterService, UpdaterService>();
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
        _splash.SourceInitialized += (_, __) =>
        {
            var area = SystemParameters.WorkArea;
            _splash.Left = area.Left + (area.Width - _splash.Width) / 2;
            _splash.Top = area.Top + (area.Height - _splash.Height) / 2;
        };
        _splash.Show();
        _splash.UpdateStatus("Starting…");

        var sw = Stopwatch.StartNew();
        long Mark(string label)
        {
            var ms = sw.ElapsedMilliseconds;
            _logger?.Write(AppLogLevel.Debug, "Startup", $"{label} @ {ms} ms");
            _splash?.UpdateStatus(label + "…");
            return ms;
        }

        // 2. Storage Directory Check
        Mark("Verifying storage folders");
        if (!StorageBootstrapper.TryEnsureCoreDirs(out var dirError))
        {
            try { _splash?.Close(); _splash = null; } catch { }

            MessageBox.Show(
                $"Critical Error initializing local storage folders:\n\n{dirError}\n\nThe application will now close.",
                "Storage Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
            return;
        }

        // 3. Configure Dependency Injection & Services
        Mark("Loading services");
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        _logger = Services.GetService<IAppLogger>();
        _logger?.Write(AppLogLevel.Info, "Startup", $"App starting v{Globals.g_AppVersion}");

        // Non-blocking cleanup of previous update temp folders
        _ = Services.GetRequiredService<IUpdaterService>().CleanupOldUpdatesAsync();

        // Brief delay to ensure smooth splash presentation
        await Task.Delay(400);

        // 4. Initialize Main Window
        Mark("Loading application");
        var mainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        // 5. Dismiss splash upon first window render
        mainWindow.ContentRendered += (_, __) =>
        {
            Mark("First window rendered");
            try
            {
                _splash?.Close();
                _splash = null;
            }
            catch { }
        };

        mainWindow.Show();
        Mark("Window shown");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Services?.GetService<IAppLogger>() is FileLogger fl)
        {
            fl.Dispose();
        }

        if (Services?.GetService<IHttpService>() is IDisposable http)
        {
            http.Dispose();
        }

        base.OnExit(e);
    }
}
