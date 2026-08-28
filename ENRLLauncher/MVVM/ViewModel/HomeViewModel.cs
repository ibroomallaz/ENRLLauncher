using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;

namespace ENRLLauncher.MVVM.ViewModel;

public class HomeViewModel : ObservableObject
{
    private readonly ILauncherService _launcherService;
    private readonly IAppLogger? _logger;

    private bool _isEditMode;
    private string _statusMessage = "All systems ready";

    public ObservableCollection<LaunchItem> Items { get; } = [];

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand LaunchItemCommand { get; }
    public ICommand AddDroppedFileCommand { get; }
    public ICommand RemoveItemCommand { get; }

    public HomeViewModel(ILauncherService launcherService, IAppLogger? logger = null)
    {
        _launcherService = launcherService ?? throw new ArgumentNullException(nameof(launcherService));
        _logger = logger;

        LaunchItemCommand = new RelayCommand(async param =>
        {
            if (param is LaunchItem item)
            {
                await LaunchAsync(item);
            }
        });

        AddDroppedFileCommand = new RelayCommand(param =>
        {
            if (param is string filePath)
            {
                AddDroppedFile(filePath);
            }
        });

        RemoveItemCommand = new RelayCommand(param =>
        {
            if (param is LaunchItem item && Items.Contains(item))
            {
                Items.Remove(item);
            }
        });
    }

    private async Task LaunchAsync(LaunchItem item)
    {
        if (IsEditMode || item == null) return;

        StatusMessage = $"Launching {item.Title}...";
        bool success = await _launcherService.LaunchAsync(item);
        StatusMessage = success ? "All systems ready" : $"Failed to launch {item.Title}";
    }

    public void AddDroppedFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var targetType = ext switch
        {
            ".pptx" or ".ppt" or ".ppsx" or ".pps" or ".pptm" => LaunchTargetType.Presentation,
            ".exe" or ".bat" or ".cmd" => LaunchTargetType.Application,
            _ => LaunchTargetType.Document
        };

        Items.Add(new LaunchItem
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            TargetPath = filePath,
            TargetType = targetType,
            SortOrder = Items.Count + 1
        });
    }
}