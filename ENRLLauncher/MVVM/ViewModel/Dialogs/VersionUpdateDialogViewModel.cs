using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model.Schema;

namespace ENRLLauncher.MVVM.ViewModel.Dialogs;

public sealed class VersionUpdateDialogViewModel : ObservableObject
{
    private readonly IUpdaterService _updaterService;
    private readonly IAppLogger? _logger;
    private readonly CurrentVersion _updateInfo;

    private bool _isDownloading;
    private string _statusText = string.Empty;

    public string TitleText { get; } = "Update Available";
    public string HeaderText { get; }
    public string SubText { get; }

    public string? DownloadUrl => _updateInfo.Location;
    public string? ChangelogText => _updateInfo.Changelog;
    public string ChangeLogUrl { get; }

    public bool HasDownload => !string.IsNullOrWhiteSpace(_updateInfo.MsiUrl) || !string.IsNullOrWhiteSpace(_updateInfo.Location);
    public bool HasNotesText => !string.IsNullOrWhiteSpace(ChangelogText);
    public bool NotesButtonEnabled => !string.IsNullOrWhiteSpace(ChangeLogUrl) || HasNotesText;

    public Visibility ChangeLogCardVisibility => HasNotesText ? Visibility.Visible : Visibility.Collapsed;

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (_isDownloading != value)
            {
                _isDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotDownloading));
            }
        }
    }

    public bool IsNotDownloading => !IsDownloading;

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand InstallUpdateCommand { get; }
    public ICommand OpenReleaseNotesCommand { get; }
    public ICommand CloseCommand { get; }

    public bool WillUpgradeRuntime => !_updaterService.IsTargetRuntimePresent(_updateInfo.RequiredDotNetVersion);
    public Visibility AdminWarningVisibility => WillUpgradeRuntime ? Visibility.Visible : Visibility.Collapsed;

    public event Action? RequestClose;
    public event Action? RequestFocusNotes;

    public VersionUpdateDialogViewModel(
        IUpdaterService updaterService,
        string installedVersion,
        CurrentVersion updateInfo,
        bool isPreRelease,
        IAppLogger? logger = null)
    {
        _updaterService = updaterService;
        _updateInfo = updateInfo;
        _logger = logger;

        HeaderText = isPreRelease
            ? $"A pre-release build is available: {updateInfo.Version}"
            : $"A new version is available: {updateInfo.Version}";
        SubText = $"Installed: {installedVersion}";

        ChangeLogUrl = updateInfo.Location?.Trim() ?? string.Empty;

        InstallUpdateCommand = new RelayCommand(_ => ExecuteInstall(), _ => HasDownload && IsNotDownloading);
        OpenReleaseNotesCommand = new RelayCommand(_ =>
        {
            if (!string.IsNullOrWhiteSpace(ChangeLogUrl))
            {
                _logger?.Write(AppLogLevel.Info, "VersionUpdateDialog", $"User opened release notes URL: {ChangeLogUrl}");
                OpenUrl(ChangeLogUrl);
            }
            else if (HasNotesText)
            {
                RequestFocusNotes?.Invoke();
            }
        }, _ => NotesButtonEnabled && IsNotDownloading);

        CloseCommand = new RelayCommand(_ =>
        {
            _logger?.Write(AppLogLevel.Info, "VersionUpdateDialog", "User closed update dialog");
            RequestClose?.Invoke();
        }, _ => IsNotDownloading);
    }

    private async void ExecuteInstall()
    {
        _logger?.Write(AppLogLevel.Info, "VersionUpdateDialog", $"User clicked 'Install Update' for version {_updateInfo.Version}");
        IsDownloading = true;
        var progress = new Progress<string>(message => StatusText = message);

        try
        {
            await _updaterService.DownloadAndInstallAsync(_updateInfo, progress);
        }
        catch (Exception ex)
        {
            StatusText = "Installation failed.";
            _logger?.Write(AppLogLevel.Error, "VersionUpdateDialog", $"Update failed: {ex.Message}");
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignored */ }
    }
}
