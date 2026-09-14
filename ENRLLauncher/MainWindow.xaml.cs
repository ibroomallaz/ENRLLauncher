using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ENRLLauncher.Core.Services;
using ENRLLauncher.MVVM.ViewModel;

namespace ENRLLauncher;

public partial class MainWindow
{
    // --- Win32 Multi-Monitor Interop Definitions ---

    // Flag for MonitorFromWindow and MonitorFromPoint to return the nearest display monitor
    // if the window or point does not intersect any display
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    // Win32 rectangle structure representing pixel coordinates on the virtual desktop
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // Win32 monitor information structure containing display boundaries and flags
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        // Must be initialized to Marshal.SizeOf<MONITORINFO>() before passing to GetMonitorInfo
        public int cbSize;

        // Bounding rectangle of the entire display monitor in virtual screen coordinates
        public RECT rcMonitor;

        // Work area rectangle of the display monitor, excluding taskbar and docked bars
        public RECT rcWork;

        // Monitor attribute flags (e.g. 1 if primary monitor)
        public uint dwFlags;
    }

    // Win32 point structure representing an (X, Y) pixel coordinate on the screen
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    // Retrieves a handle to the display monitor that has the largest area of intersection with a window handle
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    // Retrieves a handle to the display monitor that contains a specified screen point
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    // Retrieves information about a display monitor, such as its physical bounding rectangle
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // Retrieves the current cursor position in physical screen coordinates
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    // --- State Fields ---

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

    // Handles lifecycle hooks to ensure fullscreen positioning targets the current monitor
    private void InitializeWindowLifecycle()
    {
        // Re-apply fullscreen during Win32 handle creation before the window is visually presented
        SourceInitialized += (_, _) =>
        {
            if (_isFullScreen)
            {
                ApplyCurrentMonitorFullscreen();
            }
        };

        // Re-apply fullscreen once WPF visual tree layout has completed
        Loaded += (_, _) =>
        {
            if (_isFullScreen)
            {
                ApplyCurrentMonitorFullscreen();
            }
        };
    }

    // Resolves the monitor boundaries in WPF Device Independent Pixels (DIPs) where this window or cursor is situated
    private Rect GetCurrentMonitorBounds()
    {
        var helper = new WindowInteropHelper(this);
        var handle = helper.Handle;

        var monitorHandle = IntPtr.Zero;

        // 1. If the window already has a Win32 handle and is loaded, query the monitor containing the window
        if (handle != IntPtr.Zero && IsLoaded)
        {
            monitorHandle = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
        }

        // 2. If the window is not yet rendered or positioned (e.g. on early launch),
        // determine the monitor from the current physical mouse cursor position
        if (monitorHandle == IntPtr.Zero)
        {
            if (GetCursorPos(out var cursorPos))
            {
                monitorHandle = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
            }
        }

        // 3. Query the monitor dimensions and convert physical pixels to WPF DIPs
        if (monitorHandle != IntPtr.Zero)
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitorHandle, ref mi))
            {
                // Retrieve DPI scaling factor for this visual to account for high-DPI displays (e.g. 125%, 150%)
                var dpi = VisualTreeHelper.GetDpi(this);
                var dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                var dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                // Divide raw physical pixels by DPI scale to produce WPF coordinate units
                return new Rect(
                    mi.rcMonitor.Left / dpiX,
                    mi.rcMonitor.Top / dpiY,
                    (mi.rcMonitor.Right - mi.rcMonitor.Left) / dpiX,
                    (mi.rcMonitor.Bottom - mi.rcMonitor.Top) / dpiY);
            }
        }

        // Fallback to WPF primary screen parameters if monitor resolution fails
        return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
    }

    // Positions and sizes the window to completely cover the target monitor (true kiosk mode)
    private void ApplyCurrentMonitorFullscreen()
    {
        var bounds = GetCurrentMonitorBounds();

        // Switch to Manual startup location to prevent WPF from applying WorkArea centering offsets
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = WindowState.Normal;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
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
        // Preserve current window position for exit restoration (valid coordinates on any monitor, including negative coordinates)
        if (!double.IsNaN(Left) && !double.IsNaN(Top) && Width > 0 && Height > 0)
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
        }

        ApplyCurrentMonitorFullscreen();

        // Flatten outer borders for seamless kiosk edge-to-edge presentation
        WindowRootBorder.CornerRadius = new CornerRadius(0);
        WindowRootBorder.BorderThickness = new Thickness(0);

        FullScreenBtn.Content = "\U0001F5D7";
        FullScreenBtn.ToolTip = "Exit Fullscreen (Esc / F11)";
        _isFullScreen = true;
    }

    private void ExitFullScreen()
    {
        WindowState = WindowState.Normal;

        // Resolve current monitor bounds to ensure restored window remains situated on the same display
        var bounds = GetCurrentMonitorBounds();
        var targetWidth = _restoreBounds.Width > 0 ? _restoreBounds.Width : 1180;
        var targetHeight = _restoreBounds.Height > 0 ? _restoreBounds.Height : 760;

        // If the window was launched directly in fullscreen, center restored bounds on the current monitor
        var targetLeft = _restoreBounds.Width > 0 ? _restoreBounds.Left : bounds.Left + (bounds.Width - targetWidth) / 2;
        var targetTop = _restoreBounds.Height > 0 ? _restoreBounds.Top : bounds.Top + (bounds.Height - targetHeight) / 2;

        Left = targetLeft;
        Top = targetTop;
        Width = targetWidth;
        Height = targetHeight;

        // Restore standard rounded corner styling and border
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
