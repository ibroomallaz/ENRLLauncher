using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Services;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.ViewModel;

namespace ENRLLauncher;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        //Storage
        services.AddSingleton<IJsonStorageService,  JsonStorageService>();
        // Core Services
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Verify AppData directories exist before hydrating services or views
        if (!StorageBootstrapper.TryEnsureCoreDirs(out var dirError))
        {
            MessageBox.Show(
                $"Critical Error initializing local storage folders:\n\n{dirError}\n\nThe application will now close.",
                "Storage Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
            return;
        }

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}