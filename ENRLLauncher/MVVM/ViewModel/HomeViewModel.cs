using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.View.Dialogs;
using ENRLLauncher.MVVM.ViewModel.Dialogs;

namespace ENRLLauncher.MVVM.ViewModel;

public class HomeViewModel : ObservableObject
{
    private readonly ILauncherService _launcherService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILayoutService _layoutService;
    private readonly IAppStateService _appStateService;
    private readonly IAppLogger? _logger;

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
        get => _appStateService.IsEditMode;
        set => _appStateService.IsEditMode = value;
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
    public ICommand EditItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand OpenFilePickerCommand { get; }
    public ICommand AddHorizontalSeparatorCommand { get; }
    public ICommand AddLongVerticalSeparatorCommand { get; }
    public ICommand AddShortVerticalSeparatorCommand { get; }

    public HomeViewModel(
        ILauncherService launcherService,
        IFileDialogService fileDialogService,
        ILayoutService layoutService,
        IAppStateService appStateService,
        IAppLogger? logger = null)
    {
        _launcherService = launcherService ?? throw new ArgumentNullException(nameof(launcherService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
        _logger = logger;

        Items.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsDropCardVisible));

        _appStateService.PropertyChanged += OnAppStatePropertyChanged;
        _appStateService.AddSeparatorRequested += OnAddSeparatorRequested;

        LaunchItemCommand = new RelayCommand(async param =>
        {
            if (param is LaunchItem item) await LaunchAsync(item);
        });

        AddDroppedFileCommand = new RelayCommand(param =>
        {
            if (param is string filePath) AddDroppedFile(filePath);
        });

        EditItemCommand = new RelayCommand(param =>
        {
            if (param is LaunchItem item) OpenEditItemDialog(item);
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

    public void OpenEditItemDialog(LaunchItem item)
    {
        if (item == null) return;

        if (item.TargetType is LaunchTargetType.HorizontalSeparator
            or LaunchTargetType.LongVerticalSeparator
            or LaunchTargetType.ShortVerticalSeparator)
        {
            return;
        }

        var dialogVm = new EditLaunchItemDialogViewModel(item, _fileDialogService);
        var dialog = new EditLaunchItemDialog
        {
            DataContext = dialogVm,
            Owner = Application.Current?.MainWindow
        };

        dialogVm.RequestClose += () => dialog.Close();
        dialog.ShowDialog();

        if (dialogVm.Success)
        {
            RequestLayoutSave();
            _logger?.Info(nameof(HomeViewModel), $"Updated item: {item.Title}");
        }
    }

    private void OnAppStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAppStateService.IsEditMode))
        {
            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(IsDropCardVisible));
            StatusMessage = _appStateService.IsEditMode
                ? "✎ Edit Mode Active — Drag cards to swap positions, click ✎ to edit, ✕ to delete"
                : "All systems ready";

            // Immediate non-debounced flush on edit mode exit
            if (!_appStateService.IsEditMode)
            {
                _ = SaveCurrentLayoutAsync();
            }
        }
    }

    private void OnAddSeparatorRequested(LaunchTargetType type)
    {
        var defaultTitle = type switch
        {
            LaunchTargetType.HorizontalSeparator => "Section Break",
            LaunchTargetType.LongVerticalSeparator => "Long Vertical",
            LaunchTargetType.ShortVerticalSeparator => "Short Vertical",
            _ => "Separator"
        };
        AddSeparator(type, defaultTitle);
    }

    private async Task LoadInitialLayoutAsync()
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
        _logger?.Info(nameof(HomeViewModel), "Saved layout immediately (flushed on exit/edit mode change)");
    }

    public void RequestLayoutSave()
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        var token = _saveDebounceCts.Token;

        Task.Delay(1000, token).ContinueWith(async t =>
        {
            if (!t.IsCanceled)
            {
                await _layoutService.SaveLayoutAsync(Items);
                _logger?.Info(nameof(HomeViewModel), "Layout auto-saved after debounce interval");
            }
        }, TaskScheduler.Default);
    }

    public void Reorder(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Items.Count || newIndex < 0 || newIndex >= Items.Count || oldIndex == newIndex)
            return;

        Items.Move(oldIndex, newIndex);
        UpdateSortOrders();
        RequestLayoutSave();
    }

    public void SwapItems(int oldIndex, int newIndex) => Reorder(oldIndex, newIndex);

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
        if (IsEditMode || item == null || item.IsLaunching) return;

        if (item.TargetType is LaunchTargetType.HorizontalSeparator
            or LaunchTargetType.LongVerticalSeparator
            or LaunchTargetType.ShortVerticalSeparator)
        {
            return;
        }

        try
        {
            item.IsLaunching = true;
            Mouse.OverrideCursor = Cursors.AppStarting;
            StatusMessage = $"Launching {item.Title}...";

            var launchTask = _launcherService.LaunchAsync(item);
            var minDelayTask = Task.Delay(2000);

            await Task.WhenAll(launchTask, minDelayTask);
            bool success = await launchTask;

            StatusMessage = success ? "All systems ready" : $"Failed to launch {item.Title}";
        }
        catch (Exception ex)
        {
            _logger?.Error(nameof(HomeViewModel), $"Error launching {item.Title}: {ex.Message}");
            StatusMessage = $"Failed to launch {item.Title}";
        }
        finally
        {
            item.IsLaunching = false;
            Mouse.OverrideCursor = null;
        }
    }

    private void UpdateSortOrders()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].SortOrder = i + 1;
        }
    }
}
