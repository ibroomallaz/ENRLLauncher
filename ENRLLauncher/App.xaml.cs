using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
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

public partial class App : Application
{
    private const string AppMutexName = @"Local\ENRLLauncher_SingleInstance_Mutex";
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private SplashWindow? _splash;
    private IAppLogger? _logger;
    private Mutex? _singleInstanceMutex;
    private bool _hasMutexOwnership;

    public static IServiceProvider Services { get; private set; } = null!;

    private void ConfigureServices(IServiceCollection services)
    {
        // Storage
        services.AddSingleton<IJsonStorageService, JsonStorageService>();

        // Logging
        services.AddSingleton<IAppLogger>(_ => _logger ?? new FileLogger(Globals.g_LogsDir));

        // Core Services
        services.AddSingleton<IAppStateService, AppStateService>();
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<IUpdaterService, UpdaterService>();
        services.AddSingleton<VersionCheckerUI>();
        services.AddSingleton<ILayoutService, LayoutService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISecurityService, SecurityService>();
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

        // Fast-path for UAC Administrator credential verification helper
        var allArgs = Environment.GetCommandLineArgs();
        int verifyIndex = -1;
        for (int i = 0; i < allArgs.Length; i++)
        {
            if (string.Equals(allArgs[i], "--verify-admin", StringComparison.OrdinalIgnoreCase))
            {
                verifyIndex = i;
                break;
            }
        }

        if (verifyIndex >= 0)
        {
            string? token = (verifyIndex + 1 < allArgs.Length && !allArgs[verifyIndex + 1].StartsWith('-'))
                ? allArgs[verifyIndex + 1]
                : null;
            string? authFilePath = (verifyIndex + 2 < allArgs.Length && !allArgs[verifyIndex + 2].StartsWith('-'))
                ? allArgs[verifyIndex + 2]
                : null;

            int exitCode = 1;
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                bool isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
                exitCode = isElevated ? 0 : 1;

                if (isElevated)
                {
                    // 1. Signal synchronization event if token was supplied
                    if (!string.IsNullOrEmpty(token))
                    {
                        try
                        {
                            if (EventWaitHandle.TryOpenExisting($@"Global\ENRL_AdminAuth_{token}", out var evGlobal))
                            {
                                evGlobal.Set();
                                evGlobal.Dispose();
                            }
                        }
                        catch { /* ignore */ }

                        try
                        {
                            if (EventWaitHandle.TryOpenExisting($@"Local\ENRL_AdminAuth_{token}", out var evLocal))
                            {
                                evLocal.Set();
                                evLocal.Dispose();
                            }
                        }
                        catch { /* ignore */ }
                    }

                    // 2. Write authentication receipt file if requested
                    if (!string.IsNullOrEmpty(authFilePath))
                    {
                        try
                        {
                            var targetDir = Path.GetDirectoryName(authFilePath);
                            if (!string.IsNullOrEmpty(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }
                            File.WriteAllText(authFilePath, $"VERIFIED:{identity.Name}:{DateTime.UtcNow:O}");
                        }
                        catch { /* ignore */ }
                    }
                }

                try
                {
                    if (StorageBootstrapper.TryEnsureCoreDirs(out _))
                    {
                        using var fastLogger = new FileLogger(Globals.g_LogsDir);
                        fastLogger.Write(AppLogLevel.Info, "VerifyAdmin",
                            $"Elevation check helper running as '{identity.Name}'. IsInRole(Administrator)={isElevated}, Exiting with code {exitCode}");
                    }
                }
                catch
                {
                    // Ignore logger issues in fast-path
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (StorageBootstrapper.TryEnsureCoreDirs(out _))
                    {
                        using var fastLogger = new FileLogger(Globals.g_LogsDir);
                        fastLogger.Write(AppLogLevel.Error, "VerifyAdmin", $"Exception during admin role check: {ex.Message}", ex);
                    }
                }
                catch
                {
                    // Ignore logger issues in fast-path
                }
            }

            Environment.Exit(exitCode);
            return;
        }

        // Enforce single running application instance
        try
        {
            _singleInstanceMutex = new Mutex(true, AppMutexName, out _hasMutexOwnership);
        }
        catch (Exception)
        {
            // If mutex instantiation fails unexpectedly, allow the application to proceed
            _hasMutexOwnership = true;
        }

        if (!_hasMutexOwnership)
        {
            BringExistingInstanceToFront();
            Shutdown();
            return;
        }

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

        // Apply startup window preferences
        try
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            var settings = await settingsService.LoadSettingsAsync();
            if (settings?.StartInFullScreen == true)
            {
                mainWindow.ApplyFullScreen(true);
            }
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Warning, "Startup", $"Failed reading startup settings: {ex.Message}");
        }

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
                    _logger?.Write(AppLogLevel.Info, "UpdateCheck", "Triggering post-render optional update check");
                    var updateUi = Services.GetService<VersionCheckerUI>();
                    if (updateUi != null)
                    {
                        await updateUi.CheckAsync(showUpToDatePopup: false, owner: mainWindow);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Write(AppLogLevel.Warning, "UpdateCheck", $"Background update check failed: {ex.Message}");
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        };

        mainWindow.Show();
        Mark("Window shown");
    }

    private static void BringExistingInstanceToFront()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            var existingProcess = Process.GetProcessesByName(current.ProcessName)
                .FirstOrDefault(p => p.Id != current.Id);

            if (existingProcess != null && existingProcess.MainWindowHandle != IntPtr.Zero)
            {
                var handle = existingProcess.MainWindowHandle;
                if (IsIconic(handle))
                {
                    ShowWindow(handle, SW_RESTORE);
                }
                SetForegroundWindow(handle);
            }
        }
        catch
        {
            // Ignore failure when finding or focusing existing process
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Write(AppLogLevel.Info, "Shutdown", "App shutting down");

        if (_singleInstanceMutex != null)
        {
            if (_hasMutexOwnership)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Ignore if mutex was not acquired or already released
                }
            }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        if (_logger is FileLogger fl)
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
