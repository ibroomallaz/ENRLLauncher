using System.Windows;
using ENRLLauncher.Core.Services;
using ENRLLauncher.MVVM.ViewModel;

namespace ENRLLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var launcherService = new LauncherService();
        var homeVM = new HomeViewModel(launcherService);
        var settingsVM = new SettingsViewModel();
        var mainVM = new MainWindowViewModel(homeVM, settingsVM);

        var mainWindow = new MainWindow(mainVM);
        mainWindow.Show();
    }
}