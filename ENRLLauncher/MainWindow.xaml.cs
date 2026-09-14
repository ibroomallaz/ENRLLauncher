using System.Windows;
using System.Windows.Input;
using ENRLLauncher.Core.Services;
using ENRLLauncher.MVVM.ViewModel;

namespace ENRLLauncher;

public partial class MainWindow
{
    private bool _isFullScreen;
    private Rect _restoreBounds = new(100, 100, 1180, 760);

    public MainWindow()
    {
        InitializeComponent();
        InitializeWindowLifecycle();

        // Instantiate storage and domain layout dependencies
        var appStateService = new AppStateService();
        var storageService = new JsonStorageService();
        var layoutService = new LayoutService(storageService);
        var settingsService = new SettingsService(storageService);
        var launcherService = new LauncherService();
        var fileDialogService = new FileDialogService();

        var homeVM = new HomeViewModel(launcherService, fileDialogService, layoutService, appStateService);
        var settingsVM = new SettingsViewModel(settingsService, fileDialogService, storageService);
        var httpService = new HttpService();
        var updaterService = new UpdaterService(null, httpService);
        var versionCheckerUi = new VersionCheckerUI(httpService, updaterService);

        DataContext = new MainWindowViewModel(homeVM, settingsVM, appStateService, versionCheckerUi);
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        InitializeWindowLifecycle();
        DataContext = viewModel;
    }

    // Handles lifecycle hooks to ensure fullscreen positioning avoids WPF center-screen offsets
    private void InitializeWindowLifecycle()
    {
        SourceInitialized += (_, _) =>
        {
            if (_isFullScreen)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
            }
        };

        Loaded += (_, _) =>
        {
            if (_isFullScreen)
            {
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
            }
        };
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleFullScreen();
                return;
            }
            if (!_isFullScreen)
            {
                DragMove();
            }
        }
    }

    private void FullScreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullScreen();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullScreen)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
    }

    // Applies or exits fullscreen presentation kiosk mode
    public void ApplyFullScreen(bool enable = true)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (enable && !_isFullScreen)
        {
            EnterFullScreen();
        }
        else if (!enable && _isFullScreen)
        {
            ExitFullScreen();
        }
    }

    private void EnterFullScreen()
    {
        if (!double.IsNaN(Left) && Left > 0 && !double.IsNaN(Top) && Top > 0)
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = WindowState.Normal;
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        WindowRootBorder.CornerRadius = new CornerRadius(0);
        WindowRootBorder.BorderThickness = new Thickness(0);

        FullScreenBtn.Content = "\U0001F5D7";
        FullScreenBtn.ToolTip = "Exit Fullscreen (Esc / F11)";
        _isFullScreen = true;
    }

    private void ExitFullScreen()
    {
        WindowState = WindowState.Normal;
        Left = _restoreBounds.Left > 0 ? _restoreBounds.Left : 100;
        Top = _restoreBounds.Top > 0 ? _restoreBounds.Top : 100;
        Width = _restoreBounds.Width > 0 ? _restoreBounds.Width : 1180;
        Height = _restoreBounds.Height > 0 ? _restoreBounds.Height : 760;

        WindowRootBorder.CornerRadius = new CornerRadius(14);
        WindowRootBorder.BorderThickness = new Thickness(1);

        FullScreenBtn.Content = "⛶";
        FullScreenBtn.ToolTip = "Toggle Fullscreen (F11)";
        _isFullScreen = false;
    }

    private void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            EnterFullScreen();
        }
        else
        {
            ExitFullScreen();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
