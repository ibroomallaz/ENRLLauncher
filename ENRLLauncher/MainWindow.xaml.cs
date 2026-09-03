using System.Windows;
using System.Windows.Input;
using ENRLLauncher.Core.Services;
using ENRLLauncher.MVVM.ViewModel;

namespace ENRLLauncher;

public partial class MainWindow : Window
{
    private bool _isFullScreen;
    private Rect _restoreBounds;

    public MainWindow()
    {
        InitializeComponent();

        var launcherService = new LauncherService();
        var fileDialogService = new FileDialogService();

        var homeVM = new HomeViewModel(launcherService, fileDialogService);
        var settingsVM = new SettingsViewModel();

        DataContext = new MainWindowViewModel(homeVM, settingsVM);
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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

    private void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);

            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            WindowState = WindowState.Normal;

            WindowRootBorder.CornerRadius = new CornerRadius(0);
            WindowRootBorder.BorderThickness = new Thickness(0);

            FullScreenBtn.Content = "🗗";
            FullScreenBtn.ToolTip = "Exit Fullscreen (Esc / F11)";
            _isFullScreen = true;
        }
        else
        {
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