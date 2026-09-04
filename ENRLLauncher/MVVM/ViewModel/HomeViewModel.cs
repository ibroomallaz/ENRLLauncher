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
    private readonly IAppLogger? _logger;

    private bool _isEditMode;
    private string _statusMessage = "All systems ready";

    public ObservableCollection<LaunchItem> Items { get; } = [];

    // Visible only during Edit Mode, or if no cards exist yet (empty state)
    public bool IsDropCardVisible => IsEditMode || Items.Count == 0;

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

    // Commands
    public ICommand LaunchItemCommand { get; }
    public ICommand AddDroppedFileCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand OpenFilePickerCommand { get; }
    public ICommand AddHorizontalSeparatorCommand { get; }
    public ICommand AddLongVerticalSeparatorCommand { get; }
    public ICommand AddShortVerticalSeparatorCommand { get; }

    //Ctor
    public HomeViewModel(
    ILauncherService launcherService,
    IFileDialogService fileDialogService,
    IAppLogger? logger = null)
    {
        _launcherService = launcherService ?? throw new ArgumentNullException(nameof(launcherService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _logger = logger;

        Items.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(IsDropCardVisible));
        };

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
                UpdateSortOrders();
            }
        });

        OpenFilePickerCommand = new RelayCommand(_ => OpenFilePicker());

        // Separator Insertion Commands
        AddHorizontalSeparatorCommand = new RelayCommand(_ =>
            AddSeparator(LaunchTargetType.HorizontalSeparator, "Section Break"));

        AddLongVerticalSeparatorCommand = new RelayCommand(_ =>
            AddSeparator(LaunchTargetType.LongVerticalSeparator, "Full Vertical"));

        AddShortVerticalSeparatorCommand = new RelayCommand(_ =>
            AddSeparator(LaunchTargetType.ShortVerticalSeparator, "Divider"));
    }

    private void OpenFilePicker()
    {
        const string filter = "All Supported Files|*.pptx;*.ppt;*.ppsx;*.pps;*.pptm;*.exe;*.bat;*.cmd;*.ps1;*.pdf;*.docx;*.xlsx;*.txt|" +
                              "Presentations (*.pptx;*.ppt;*.ppsx)|*.pptx;*.ppt;*.ppsx;*.pps;*.pptm|" +
                              "Applications (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd;*.ps1|" +
                              "Documents (*.pdf;*.docx;*.xlsx)|*.pdf;*.docx;*.xlsx;*.txt|" +
                              "All Files (*.*)|*.*";

        var selectedFiles = _fileDialogService.OpenFiles("Select Files or Presentations to Add", filter);
        if (selectedFiles != null)
        {
            foreach (var file in selectedFiles)
            {
                AddDroppedFile(file);
            }
        }
    }

    public void Reorder(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && oldIndex < Items.Count && newIndex >= 0 && newIndex < Items.Count && oldIndex != newIndex)
        {
            Items.Move(oldIndex, newIndex);
            UpdateSortOrders();
        }
    }

    private void UpdateSortOrders()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].SortOrder = i + 1;
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

        Items.Add(new LaunchItem
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            Description = ext.TrimStart('.').ToUpperInvariant(),
            TargetPath = filePath,
            TargetType = targetType,
            SortOrder = Items.Count + 1
        });
    }
    private void AddSeparator(LaunchTargetType type, string defaultTitle)
    {
        var item = new LaunchItem
        {
            Title = defaultTitle,
            TargetType = type,
            SortOrder = Items.Count + 1
        };

        Items.Add(item);
        UpdateSortOrders();
    }

    private async Task LaunchAsync(LaunchItem item)
    {
        if (IsEditMode || item == null) return;
        // Ignore clicks on any separator type
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
}