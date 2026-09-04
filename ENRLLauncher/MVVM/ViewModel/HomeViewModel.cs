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
    private readonly IFileDialogService _fileDialogService;
    private readonly ILayoutService _layoutService;
    private readonly IAppLogger? _logger;

    private bool _isEditMode;
    private string _statusMessage = "All systems ready";

    public ObservableCollection<LaunchItem> Items { get; } = [];

    public static bool IsDropCardVisible => true;

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
                StatusMessage = _isEditMode
                    ? "✏ Edit Mode Active — Drag cards to swap positions, click ✕ to delete"
                    : "All systems ready";

                // Persist changes (such as inline row break title edits) when exiting edit mode
                if (!_isEditMode)
                {
                    _ = SaveCurrentLayoutAsync();
                }
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
    public ICommand OpenFilePickerCommand { get; }
    public ICommand AddHorizontalSeparatorCommand { get; }
    public ICommand AddLongVerticalSeparatorCommand { get; }
    public ICommand AddShortVerticalSeparatorCommand { get; }

    public HomeViewModel(
        ILauncherService launcherService,
        IFileDialogService fileDialogService,
        ILayoutService layoutService,
        IAppLogger? logger = null)
    {
        _launcherService = launcherService ?? throw new ArgumentNullException(nameof(launcherService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _logger = logger;

        Items.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsDropCardVisible));

        LaunchItemCommand = new RelayCommand(async param =>
        {
            if (param is LaunchItem item) await LaunchAsync(item);
        });

        AddDroppedFileCommand = new RelayCommand(param =>
        {
            if (param is string filePath) AddDroppedFile(filePath);
        });

        RemoveItemCommand = new RelayCommand(async param =>
        {
            if (param is LaunchItem item && Items.Contains(item))
            {
                Items.Remove(item);
                UpdateSortOrders();
                await SaveCurrentLayoutAsync();
                _logger?.Log(AppLogLevel.Info, $"Removed item: {item.Title}");
            }
        });

        OpenFilePickerCommand = new RelayCommand(_ => OpenFilePicker());

        AddHorizontalSeparatorCommand = new RelayCommand(async _ =>
            await AddSeparatorAsync(LaunchTargetType.HorizontalSeparator, "Section Break"));

        AddLongVerticalSeparatorCommand = new RelayCommand(async _ =>
            await AddSeparatorAsync(LaunchTargetType.LongVerticalSeparator, "Long Vertical"));

        AddShortVerticalSeparatorCommand = new RelayCommand(async _ =>
            await AddSeparatorAsync(LaunchTargetType.ShortVerticalSeparator, "Short Vertical"));

        // Load saved canvas layout from local appdata on startup
        _ = LoadInitialLayoutAsync();
    }

    public async Task LoadInitialLayoutAsync()
    {
        var savedItems = await _layoutService.LoadLayoutAsync();
        Items.Clear();

        foreach (var item in savedItems)
        {
            Items.Add(item);
        }

        UpdateSortOrders();
    }

    public async Task SaveCurrentLayoutAsync()
    {
        await _layoutService.SaveLayoutAsync(Items);
    }

    public void Reorder(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && oldIndex < Items.Count && newIndex >= 0 && newIndex < Items.Count && oldIndex != newIndex)
        {
            Items.Move(oldIndex, newIndex);
            UpdateSortOrders();
            _ = SaveCurrentLayoutAsync();
        }
    }

    public void AddDroppedFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var targetType = ext switch
        {
            ".pptx" or ".ppt" or ".ppsx" or ".pps" or ".pptm" => LaunchTargetType.Presentation,
            ".exe" or ".bat" or ".cmd" or ".ps1" => LaunchTargetType.Application,
            _ => LaunchTargetType.Document
        };

        var newItem = new LaunchItem
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            Description = ext.TrimStart('.').ToUpperInvariant(),
            TargetPath = filePath,
            TargetType = targetType,
            SortOrder = Items.Count + 1
        };

        Items.Add(newItem);
        UpdateSortOrders();
        _ = SaveCurrentLayoutAsync();
        _logger?.Log(AppLogLevel.Info, $"Added file {filePath} ({targetType})");
    }

    private async Task AddSeparatorAsync(LaunchTargetType type, string defaultTitle)
    {
        var separatorItem = new LaunchItem
        {
            Title = defaultTitle,
            TargetType = type,
            SortOrder = Items.Count + 1
        };

        Items.Add(separatorItem);
        UpdateSortOrders();
        await SaveCurrentLayoutAsync();
        _logger?.Log(AppLogLevel.Info, $"Added separator {type}");
    }

    private void OpenFilePicker()
    {
        const string filter = "All Supported Files|*.pptx;*.ppt;*.ppsx;*.pps;*.pptm;*.exe;*.bat;*.cmd;*.ps1;*.pdf;*.docx;*.xlsx;*.txt|" +
                              "Presentations (*.pptx;*.ppt;*.ppsx)|*.pptx;*.ppt;*.ppsx;*.pps;*.pptm|" +
                              "Applications (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd;*.ps1|" +
                              "Documents (*.pdf;*.docx;*.xlsx)|*.pdf;*.docx;*.xlsx;*.txt|" +
                              "All Files (*.*)|*.*";

        var selectedFiles = _fileDialogService.OpenFiles("Select Items to Add", filter);
        if (selectedFiles != null)
        {
            foreach (var file in selectedFiles)
            {
                AddDroppedFile(file);
            }
        }
    }

    private async Task LaunchAsync(LaunchItem item)
    {
        if (IsEditMode || item == null) return;

        if (item.TargetType is LaunchTargetType.HorizontalSeparator
                            or LaunchTargetType.LongVerticalSeparator
                            or LaunchTargetType.ShortVerticalSeparator)
        {
            return;
        }

        StatusMessage = $"Launching {item.Title}...";
        bool success = await _launcherService.LaunchAsync(item);
        StatusMessage = success ? "All systems ready" : $"Failed to launch {item.Title}";
    }

    private void UpdateSortOrders()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].SortOrder = i + 1;
        }
    }
}