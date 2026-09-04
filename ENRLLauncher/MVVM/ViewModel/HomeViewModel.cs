using System.Collections.ObjectModel;
using System.IO;
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
    private CancellationTokenSource? _saveDebounceCts;

    public ObservableCollection<LaunchItem> Items { get; } = [];

    // Visible in Edit Mode, or during onboarding when 1 or fewer launch options exist
    public bool IsDropCardVisible => IsEditMode || LaunchableCount <= 1;

    private int LaunchableCount => Items.Count(i =>
        i.TargetType is not (LaunchTargetType.HorizontalSeparator
                          or LaunchTargetType.LongVerticalSeparator
                          or LaunchTargetType.ShortVerticalSeparator));

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDropCardVisible));
                StatusMessage = _isEditMode
                    ? "✏ Edit Mode Active — Drag cards to swap positions, click ✕ to delete"
                    : "All systems ready";

                // Immediate non-debounced flush on edit mode exit
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

        RemoveItemCommand = new RelayCommand(param =>
        {
            if (param is LaunchItem item && Items.Contains(item))
            {
                Items.Remove(item);
                UpdateSortOrders();
                RequestLayoutSave();
                _logger?.Info(nameof(HomeViewModel), $"Removed item: {item.Title}");
            }
        });

        OpenFilePickerCommand = new RelayCommand(_ => OpenFilePicker());

        AddHorizontalSeparatorCommand = new RelayCommand(_ =>
            AddSeparator(LaunchTargetType.HorizontalSeparator, "Section Break"));

        AddLongVerticalSeparatorCommand = new RelayCommand(_ =>
            AddSeparator(LaunchTargetType.LongVerticalSeparator, "Long Vertical"));

        AddShortVerticalSeparatorCommand = new RelayCommand(_ =>
            AddSeparator(LaunchTargetType.ShortVerticalSeparator, "Short Vertical"));

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
        OnPropertyChanged(nameof(IsDropCardVisible));
    }

    public async Task SaveCurrentLayoutAsync()
    {
        await _layoutService.SaveLayoutAsync(Items);
    }

    public void RequestLayoutSave()
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        var token = _saveDebounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token);
                if (!token.IsCancellationRequested)
                {
                    await SaveCurrentLayoutAsync();
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    public void Reorder(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && oldIndex < Items.Count && newIndex >= 0 && newIndex < Items.Count && oldIndex != newIndex)
        {
            Items.Move(oldIndex, newIndex);
            UpdateSortOrders();
            RequestLayoutSave();
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
        RequestLayoutSave();
        _logger?.Info(nameof(HomeViewModel), $"Added file {filePath} ({targetType})");
    }

    private void AddSeparator(LaunchTargetType type, string defaultTitle)
    {
        var separatorItem = new LaunchItem
        {
            Title = defaultTitle,
            TargetType = type,
            SortOrder = Items.Count + 1
        };

        Items.Add(separatorItem);
        UpdateSortOrders();
        RequestLayoutSave();
        _logger?.Info(nameof(HomeViewModel), $"Added separator {type}");
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