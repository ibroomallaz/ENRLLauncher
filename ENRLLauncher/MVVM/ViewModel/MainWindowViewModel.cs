using System;
using System.Windows.Input;
using System.Windows.Threading;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;

namespace ENRLLauncher.MVVM.ViewModel;

public class MainWindowViewModel : ObservableObject
{
    private readonly IAppLogger? _logger;
    private readonly DispatcherTimer _clockTimer;

    private object _currentView;
    private string _currentTime = string.Empty;
    private bool _isEditMode;
    private bool _isCompactMode;

    public HomeViewModel HomeVM { get; }
    public SettingsViewModel SettingsVM { get; }

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

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
                HomeVM.IsEditMode = value;
            }
        }
    }

    public bool IsCompactMode
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

    public MainWindowViewModel(HomeViewModel homeVM, SettingsViewModel settingsVM, IAppLogger? logger = null)
    {
        HomeVM = homeVM ?? throw new ArgumentNullException(nameof(homeVM));
        SettingsVM = settingsVM ?? throw new ArgumentNullException(nameof(settingsVM));
        _logger = logger;
        _currentView = HomeVM;

        NavigateHomeCommand = new RelayCommand(_ => CurrentView = HomeVM);
        NavigateSettingsCommand = new RelayCommand(_ => CurrentView = SettingsVM);
        ToggleEditModeCommand = new RelayCommand(_ => IsEditMode = !IsEditMode);
        ToggleCompactModeCommand = new RelayCommand(_ => IsCompactMode = !IsCompactMode);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("h:mm tt");
        _clockTimer.Start();
        CurrentTime = DateTime.Now.ToString("h:mm tt");
    }
}