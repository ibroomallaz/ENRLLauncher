using System.Collections.ObjectModel;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;

namespace ENRLLauncher.MVVM.ViewModel;

public class SettingsViewModel : ObservableObject
{
    private readonly IAppLogger? _logger;

    private string _preferredStartupMode = "Maximized";
    private bool _alwaysOnTopInCompact;
    private bool _autoCloseOnLaunch;

    public ObservableCollection<LaunchItem> Items { get; } = [];

    public string PreferredStartupMode
    {
        get => _preferredStartupMode;
        set
        {
            if (_preferredStartupMode != value)
            {
                _preferredStartupMode = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AlwaysOnTopInCompact
    {
        get => _alwaysOnTopInCompact;
        set
        {
            if (_alwaysOnTopInCompact != value)
            {
                _alwaysOnTopInCompact = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AutoCloseOnLaunch
    {
        get => _autoCloseOnLaunch;
        set
        {
            if (_autoCloseOnLaunch != value)
            {
                _autoCloseOnLaunch = value;
                OnPropertyChanged();
            }
        }
    }

    public SettingsViewModel(IAppLogger? logger = null)
    {
        _logger = logger;
    }
}