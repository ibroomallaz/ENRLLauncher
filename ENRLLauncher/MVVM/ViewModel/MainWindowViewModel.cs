using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Services;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.Model.Schema;

namespace ENRLLauncher.MVVM.ViewModel;

public class MainWindowViewModel : ObservableObject
{
    private readonly VersionCheckerUI _versionCheckerUi;
    private readonly IAppLogger? _logger;
    private readonly DispatcherTimer _clockTimer;

    private object _currentView;
    private string _currentTime = string.Empty;
    private bool _isEditMode;
    private bool _isCompactMode;

    private bool _isCheckingForUpdates;
    private bool _isUpdateAvailable;
    private string? _availableVersion;
    private string _versionDisplayText = $"v{Globals.g_AppVersion}";
    private string _versionToolTip = $"Version v{Globals.g_AppVersion}\nClick to check for updates.";

    public string AppVersion => Globals.g_AppVersion;
    private HomeViewModel HomeVM { get; }
    private SettingsViewModel SettingsVM { get; }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (_isCheckingForUpdates != value)
            {
                _isCheckingForUpdates = value;
                OnPropertyChanged();
                UpdateVersionDisplay();
            }
        }
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set
        {
            if (_isUpdateAvailable != value)
            {
                _isUpdateAvailable = value;
                OnPropertyChanged();
                UpdateVersionDisplay();
            }
        }
    }

    public string VersionDisplayText
    {
        get => _versionDisplayText;
        private set
        {
            if (_versionDisplayText != value)
            {
                _versionDisplayText = value;
                OnPropertyChanged();
            }
        }
    }

    public string VersionToolTip
    {
        get => _versionToolTip;
        private set
        {
            if (_versionToolTip != value)
            {
                _versionToolTip = value;
                OnPropertyChanged();
            }
        }
    }

    public object CurrentView
    {
        get => _currentView;
        set
        {
            if (_currentView != value)
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_currentTime != value)
            {
                _currentTime = value;
                OnPropertyChanged();
            }
        }
    }

    private bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
                HomeVM.IsEditMode = value; // Sync edit mode state to active Home view
            }
        }
    }

    private bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            if (_isCompactMode != value)
            {
                _isCompactMode = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand NavigateHomeCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand ToggleEditModeCommand { get; }
    public ICommand ToggleCompactModeCommand { get; }
    public ICommand CheckUpdateCommand { get; }

    public MainWindowViewModel(
        HomeViewModel homeVM,
        SettingsViewModel settingsVM,
        VersionCheckerUI? versionCheckerUi = null,
        IAppLogger? logger = null)
    {
        HomeVM = homeVM ?? throw new ArgumentNullException(nameof(homeVM));
        SettingsVM = settingsVM ?? throw new ArgumentNullException(nameof(settingsVM));
        _versionCheckerUi = versionCheckerUi ?? new VersionCheckerUI(new HttpService(), new UpdaterService());
        _logger = logger;
        _currentView = HomeVM;

        _versionCheckerUi.CheckingStateChanged += OnCheckingStateChanged;
        _versionCheckerUi.UpdateAvailabilityChanged += OnUpdateAvailabilityChanged;

        if (_versionCheckerUi is { IsUpdateAvailable: true, AvailableUpdate: not null })
        {
            OnUpdateAvailabilityChanged(true, _versionCheckerUi.AvailableUpdate);
        }
        else
        {
            UpdateVersionDisplay();
        }

        NavigateHomeCommand = new RelayCommand(_ => CurrentView = HomeVM);
        NavigateSettingsCommand = new RelayCommand(_ => CurrentView = SettingsVM);
        ToggleEditModeCommand = new RelayCommand(_ => IsEditMode = !IsEditMode);
        ToggleCompactModeCommand = new RelayCommand(_ => IsCompactMode = !IsCompactMode);
        CheckUpdateCommand = new RelayCommand(_ => ExecuteCheckUpdate(), _ => !IsCheckingForUpdates);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("h:mm tt");
        _clockTimer.Start();
        CurrentTime = DateTime.Now.ToString("h:mm tt");
    }

    private void OnCheckingStateChanged(bool isChecking)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => OnCheckingStateChanged(isChecking));
            return;
        }

        IsCheckingForUpdates = isChecking;
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnUpdateAvailabilityChanged(bool available, CurrentVersion? update)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => OnUpdateAvailabilityChanged(available, update));
            return;
        }

        _availableVersion = update?.Version;
        IsUpdateAvailable = available;
    }

    private void UpdateVersionDisplay()
    {
        if (IsCheckingForUpdates)
        {
            VersionDisplayText = $"v{Globals.g_AppVersion}";
            VersionToolTip = "Checking for updates…";
        }
        else if (IsUpdateAvailable && !string.IsNullOrWhiteSpace(_availableVersion))
        {
            VersionDisplayText = $"v{Globals.g_AppVersion}";
            VersionToolTip = $"New update available: v{_availableVersion}\nInstalled: v{Globals.g_AppVersion}\nClick to review and install update.";
        }
        else
        {
            VersionDisplayText = $"v{Globals.g_AppVersion}";
            VersionToolTip = $"Version v{Globals.g_AppVersion}\nClick to check for updates.";
        }
    }

    private async void ExecuteCheckUpdate()
    {
        try
        {
            await HandleCheckUpdateAsync();
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Error, "MainWindow", $"Unhandled exception in CheckUpdateCommand: {ex.Message}");
        }
    }

    private async Task HandleCheckUpdateAsync()
    {
        try
        {
            _logger?.Write(AppLogLevel.Info, "MainWindow", "Version tab clicked by user to check/initiate update");
            var owner = Application.Current?.MainWindow;

            if (_versionCheckerUi is { IsUpdateAvailable: true, AvailableUpdate: not null })
            {
                _versionCheckerUi.OpenUpdateDialog(owner);
                return;
            }

            await _versionCheckerUi.CheckAsync(showUpToDatePopup: true, owner: owner, forceShowDialog: true);
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Warning, "MainWindow", $"CheckUpdateCommand error: {ex.Message}");
        }
        finally
        {
            UpdateVersionDisplay();
        }
    }
}
