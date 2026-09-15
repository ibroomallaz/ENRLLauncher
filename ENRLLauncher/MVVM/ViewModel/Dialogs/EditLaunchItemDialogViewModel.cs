using System.IO;
using System.Windows.Input;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;

namespace ENRLLauncher.MVVM.ViewModel.Dialogs;

public class EditLaunchItemDialogViewModel : ObservableObject
{
    private readonly LaunchItem _originalItem;
    private readonly IFileDialogService _fileDialogService;

    private string _title;
    private string _description;
    private string? _customBadgeText;
    private string _targetPath;
    private string? _arguments;
    private LaunchTargetType _targetType;
    private string? _errorMessage;

    public event Action? RequestClose;

    public bool Success { get; private set; }

    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
            {
                Validate();
            }
        }
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value);
    }

    public string? CustomBadgeText
    {
        get => _customBadgeText;
        set => Set(ref _customBadgeText, value);
    }

    public string TargetPath
    {
        get => _targetPath;
        set
        {
            if (Set(ref _targetPath, value))
            {
                Validate();
            }
        }
    }

    public string? Arguments
    {
        get => _arguments;
        set => Set(ref _arguments, value);
    }

    public LaunchTargetType TargetType
    {
        get => _targetType;
        set
        {
            if (Set(ref _targetType, value))
            {
                OnPropertyChanged(nameof(DefaultBadgePlaceholder));
            }
        }
    }

    public string DefaultBadgePlaceholder => $"Default: {_targetType}";

    public IReadOnlyList<LaunchTargetType> AvailableTargetTypes { get; } =
    [
        LaunchTargetType.Presentation,
        LaunchTargetType.Application,
        LaunchTargetType.Document,
        LaunchTargetType.WebLink
    ];

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (Set(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseCommand { get; }

    public EditLaunchItemDialogViewModel(LaunchItem item, IFileDialogService fileDialogService)
    {
        _originalItem = item ?? throw new ArgumentNullException(nameof(item));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));

        _title = item.Title;
        _description = item.Description;
        _customBadgeText = item.CustomBadgeText;
        _targetPath = item.TargetPath;
        _arguments = item.Arguments;
        _targetType = item.TargetType;

        SaveCommand = new RelayCommand(_ => Save());
        CancelCommand = new RelayCommand(_ => Cancel());
        BrowseCommand = new RelayCommand(_ => BrowseFile());
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Title cannot be empty.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetPath))
        {
            ErrorMessage = "Target path or URL cannot be empty.";
            return;
        }

        ErrorMessage = null;
    }

    private void BrowseFile()
    {
        const string filter = "All Supported Files|*.pptx;*.ppt;*.ppsx;*.pps;*.pptm;*.exe;*.bat;*.cmd;*.ps1;*.pdf;*.docx;*.xlsx;*.txt|" +
                              "Presentations (*.pptx;*.ppt;*.ppsx)|*.pptx;*.ppt;*.ppsx;*.pps;*.pptm|" +
                              "Applications (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd;*.ps1|" +
                              "Documents (*.pdf;*.docx;*.xlsx)|*.pdf;*.docx;*.xlsx;*.txt|" +
                              "All Files (*.*)|*.*";

        var selected = _fileDialogService.OpenFile("Select Target File", filter);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            TargetPath = selected;
            var ext = Path.GetExtension(selected).ToLowerInvariant();
            TargetType = ext switch
            {
                ".pptx" or ".ppt" or ".ppsx" or ".pps" or ".pptm" => LaunchTargetType.Presentation,
                ".exe" or ".bat" or ".cmd" or ".ps1" => LaunchTargetType.Application,
                _ => LaunchTargetType.Document
            };
        }
    }

    private void Save()
    {
        Validate();
        if (HasError) return;

        _originalItem.Title = Title.Trim();
        _originalItem.Description = Description?.Trim() ?? string.Empty;
        _originalItem.CustomBadgeText = string.IsNullOrWhiteSpace(CustomBadgeText) ? null : CustomBadgeText.Trim();
        _originalItem.TargetPath = TargetPath.Trim();
        _originalItem.Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim();
        _originalItem.TargetType = TargetType;

        Success = true;
        RequestClose?.Invoke();
    }

    private void Cancel()
    {
        Success = false;
        RequestClose?.Invoke();
    }
}
