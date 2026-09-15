using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Utilities;
using Newtonsoft.Json;

namespace ENRLLauncher.MVVM.Model;

public class LaunchItem : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string? _customBadgeText;
    private string _targetPath = string.Empty;
    private string? _arguments;
    private string? _iconPath;
    private LaunchTargetType _targetType = LaunchTargetType.Presentation;
    private int _sortOrder;
    private bool _isEnabled = true;
    private bool _isLaunching;

    public Guid Id
    {
        get => _id;
        set => Set(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value);
    }

    public string? CustomBadgeText
    {
        get => _customBadgeText;
        set
        {
            if (Set(ref _customBadgeText, value))
            {
                OnPropertyChanged(nameof(DisplayBadgeText));
            }
        }
    }

    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayBadgeText =>
        !string.IsNullOrWhiteSpace(_customBadgeText)
            ? _customBadgeText
            : _targetType.ToString();

    public string TargetPath
    {
        get => _targetPath;
        set => Set(ref _targetPath, value);
    }

    public string? Arguments
    {
        get => _arguments;
        set => Set(ref _arguments, value);
    }

    public string? IconPath
    {
        get => _iconPath;
        set => Set(ref _iconPath, value);
    }

    public LaunchTargetType TargetType
    {
        get => _targetType;
        set
        {
            if (Set(ref _targetType, value))
            {
                OnPropertyChanged(nameof(DisplayBadgeText));
            }
        }
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => Set(ref _sortOrder, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }

    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLaunching
    {
        get => _isLaunching;
        set => Set(ref _isLaunching, value);
    }
}
