using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Services;
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
        // Core Services
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

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}