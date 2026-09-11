using System.Windows;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.Model.Schema;
using ENRLLauncher.MVVM.View.Dialogs;
using ENRLLauncher.MVVM.ViewModel.Dialogs;

namespace ENRLLauncher.Core.Services;

public class VersionCheckerUI
{
    private readonly IHttpService _http;
    private readonly IUpdaterService _updaterService;
    private readonly IAppLogger? _logger;

    private readonly string _installedVersion = Globals.g_AppVersion;

    private const string Cat = "Version.UI";
    private const string RequiredCat = "Version.Required";

    private VersionCheckResult? _cached;
    private DateTime _lastFetchUtc;
    private readonly TimeSpan _cacheWindow = TimeSpan.FromMinutes(2);
    private string? _lastPromptedStable;
    private string? _lastPromptedPre;

    public bool IsChecking { get; private set; }
    public bool IsUpdateAvailable { get; private set; }
    public CurrentVersion? AvailableUpdate { get; private set; }
    public bool IsPreRelease { get; private set; }

    public event Action<bool>? CheckingStateChanged;
    public event Action<bool, CurrentVersion?>? UpdateAvailabilityChanged;

    public VersionCheckerUI(IHttpService http, IUpdaterService updater, IAppLogger? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _updaterService = updater ?? throw new ArgumentNullException(nameof(updater));
        _logger = logger;
    }

    public async Task EnforceRequiredAsync()
    {
        SetChecking(true);
        try
        {
            const string url = Globals.g_VersionJSON;
            _logger?.Write(AppLogLevel.Info, RequiredCat, $"enforce.start installed=\"{_installedVersion}\" url=\"{url}\"");

            var res = await FetchAsync();
            if (res is not { Success: true } && res?.Info == null && res is not { HasAnyStable: true, HasAnyPre: true })
            {
                _logger?.Write(AppLogLevel.Info, RequiredCat, "enforce.skip no-data");
                return;
            }

            var minReq = res.RequiredMinVersion;
            if (string.IsNullOrWhiteSpace(minReq))
            {
                _logger?.Write(AppLogLevel.Info, RequiredCat, "enforce.skip no-required");
                return;
            }

            var mustUpdate = VersionChecker.IsNewerVersion(_installedVersion, minReq);
            _logger?.Write(AppLogLevel.Info, RequiredCat, $"enforce.eval installed=\"{_installedVersion}\" required.min=\"{minReq}\" mustUpdate={mustUpdate}");

            if (!mustUpdate) return;

            var msg = res.RequiredMessage ?? "A newer version of the launcher is required to continue.";
            var updatePayload = res.Info?.Current ?? new CurrentVersion
            {
                Location = res.StableLocation,
                Version = res.StableVersion,
                MsiUrl = res.StableMsiUrl,
                SetupUrl = res.StableSetupUrl,
                RequiredDotNetVersion = res.StableRequiredDotNetVersion
            };

            _logger?.Write(AppLogLevel.Warning, RequiredCat, $"Mandatory update triggered: minVersion={minReq}");
            await ShowRequiredBlockingAsync(GetPreferredOwner(), minReq, msg, updatePayload);
        }
        finally
        {
            SetChecking(false);
        }
    }

    public async Task CheckAsync(bool showUpToDatePopup = false, Window? owner = null, bool forceShowDialog = false)
    {
        SetChecking(true);
        try
        {
            const string url = Globals.g_VersionJSON;
            _logger?.Write(AppLogLevel.Info, Cat, $"check.start installed=\"{_installedVersion}\" url=\"{url}\"");

            var res = await FetchAsync();
            if (res is not { Success: true } && res?.Info == null && res is not { HasAnyStable: true, HasAnyPre: true })
            {
                _logger?.Write(AppLogLevel.Warning, Cat, $"check.error: {res?.Error ?? "Unknown error"}");
                return;
            }

            var stablePayload = res.Info?.Current ?? new CurrentVersion
            {
                Version = res.StableVersion,
                Location = res.StableLocation,
                Changelog = res.StableChangelog,
                MsiUrl = res.StableMsiUrl,
                SetupUrl = res.StableSetupUrl,
                RequiredDotNetVersion = res.StableRequiredDotNetVersion
            };

            var prePayload = new CurrentVersion
            {
                Version = res.Info?.PreRelease?.Version ?? res.PreVersion,
                Location = res.Info?.PreRelease?.Location ?? res.PreLocation,
                Changelog = res.Info?.PreRelease?.Changelog ?? res.PreChangelog,
                MsiUrl = res.Info?.PreRelease?.MsiUrl ?? res.PreMsiUrl,
                SetupUrl = res.Info?.PreRelease?.SetupUrl ?? res.PreSetupUrl,
                RequiredDotNetVersion = res.Info?.PreRelease?.RequiredDotNetVersion ?? res.PreRequiredDotNetVersion
            };

            var preExists = res.Info?.PreRelease?.Exists ?? res.PreExists;

            if (!string.IsNullOrWhiteSpace(res.RequiredMinVersion) &&
                VersionChecker.IsNewerVersion(_installedVersion, res.RequiredMinVersion))
            {
                var msg = res.RequiredMessage ?? "A newer version is required to continue.";
                _logger?.Write(AppLogLevel.Warning, RequiredCat, $"CheckAsync found obsolete version, blocking for update: minVersion={res.RequiredMinVersion}");
                await ShowRequiredBlockingAsync(owner ?? GetPreferredOwner(), res.RequiredMinVersion, msg, stablePayload);
                return;
            }

            var newerStable = !string.IsNullOrWhiteSpace(stablePayload.Version) && VersionChecker.IsNewerVersion(_installedVersion, stablePayload.Version);
            var newerPre = preExists && !string.IsNullOrWhiteSpace(prePayload.Version) && VersionChecker.IsNewerVersion(_installedVersion, prePayload.Version);

            if (newerStable)
            {
                IsUpdateAvailable = true;
                AvailableUpdate = stablePayload;
                IsPreRelease = false;
                UpdateAvailabilityChanged?.Invoke(true, stablePayload);
            }
            else if (newerPre)
            {
                IsUpdateAvailable = true;
                AvailableUpdate = prePayload;
                IsPreRelease = true;
                UpdateAvailabilityChanged?.Invoke(true, prePayload);
            }
            else
            {
                IsUpdateAvailable = false;
                AvailableUpdate = null;
                IsPreRelease = false;
                UpdateAvailabilityChanged?.Invoke(false, null);
            }

            var showed = ShowPopupIfNewer(stablePayload, preExists, prePayload, owner, forceShowDialog);
            if (!showed && showUpToDatePopup)
            {
                var targetOwner = owner ?? GetPreferredOwner();
                var msgBoxText = $"You are up to date. Version: ({_installedVersion}).";
                if (targetOwner != null)
                    MessageBox.Show(targetOwner, msgBoxText, "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show(msgBoxText, "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);

                _logger?.Write(AppLogLevel.Info, Cat, "check.up-to-date.shown");
            }
        }
        finally
        {
            SetChecking(false);
        }
    }

    private void SetChecking(bool checking)
    {
        IsChecking = checking;
        CheckingStateChanged?.Invoke(checking);
    }

    public void OpenUpdateDialog(Window? owner = null)
    {
        if (!IsUpdateAvailable || AvailableUpdate == null) return;
        _logger?.Write(AppLogLevel.Info, Cat, $"Manually opening update dialog for {AvailableUpdate.Version}");
        ShowUpdateDialog(owner ?? GetPreferredOwner(), AvailableUpdate, IsPreRelease);
    }

    private async Task<VersionCheckResult?> FetchAsync()
    {
        if (_cached != null && DateTime.UtcNow - _lastFetchUtc < _cacheWindow)
        {
            _logger?.Write(AppLogLevel.Debug, Cat, "fetch.cache.hit");
            return _cached;
        }

        var res = await VersionChecker.CheckVersionAsync(Globals.g_VersionJSON, _http, _logger);
        if (res.Success || res.Info != null || res.HasAnyStable || res.HasAnyPre)
        {
            _cached = res;
            _lastFetchUtc = DateTime.UtcNow;
            _logger?.Write(AppLogLevel.Debug, Cat, "fetch.cache.store");
        }
        return res;
    }

    private bool ShowPopupIfNewer(CurrentVersion stable, bool preExists, CurrentVersion pre, Window? owner, bool forceShow = false)
    {
        var newerStable = !string.IsNullOrWhiteSpace(stable.Version) && VersionChecker.IsNewerVersion(_installedVersion, stable.Version);
        var newerPre = preExists && !string.IsNullOrWhiteSpace(pre.Version) && VersionChecker.IsNewerVersion(_installedVersion, pre.Version);

        _logger?.Write(AppLogLevel.Info, Cat, $"decide newer.stable={newerStable} newer.pre={newerPre}");

        var targetOwner = owner ?? GetPreferredOwner();

        if (newerStable)
        {
            if (!forceShow && string.Equals(_lastPromptedStable, stable.Version, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.Write(AppLogLevel.Info, Cat, "popup.stable.skip duplicate");
                return false;
            }

            _logger?.Write(AppLogLevel.Info, Cat, $"Showing update modal for stable release: {stable.Version}");
            ShowUpdateDialog(targetOwner, stable, isPreRelease: false);
            _lastPromptedStable = stable.Version;
            return true;
        }

        if (newerPre)
        {
            if (!forceShow && string.Equals(_lastPromptedPre, pre.Version, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.Write(AppLogLevel.Info, Cat, "popup.pre.skip duplicate");
                return false;
            }

            _logger?.Write(AppLogLevel.Info, Cat, $"Showing update modal for pre-release build: {pre.Version}");
            ShowUpdateDialog(targetOwner, pre, isPreRelease: true);
            _lastPromptedPre = pre.Version;
            return true;
        }

        return false;
    }

    private void ShowUpdateDialog(Window? owner, CurrentVersion updateInfo, bool isPreRelease)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var viewModel = new VersionUpdateDialogViewModel(
                _updaterService,
                _installedVersion,
                updateInfo,
                isPreRelease,
                _logger);

            var dialog = new VersionUpdateDialog
            {
                DataContext = viewModel,
                Owner = owner ?? GetPreferredOwner()
            };

            viewModel.RequestClose += () => dialog.Close();
            dialog.ShowDialog();
        });
    }

    private async Task ShowRequiredBlockingAsync(Window? owner, string minVersion, string message, CurrentVersion updateInfo)
    {
        var text =
            "This version of the launcher is no longer supported.\n\n" +
            $"Minimum required version: {minVersion}\n\n" +
            message;

        var targetOwner = owner ?? GetPreferredOwner();
        var result = targetOwner != null
            ? MessageBox.Show(targetOwner, text, "Update Required", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
            : MessageBox.Show(text, "Update Required", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

        if (result == MessageBoxResult.OK)
        {
            _logger?.Write(AppLogLevel.Info, RequiredCat, "User accepted required update prompt, commencing download");
            await _updaterService.DownloadAndInstallAsync(updateInfo);
        }
        else
        {
            _logger?.Write(AppLogLevel.Warning, RequiredCat, "User declined required update prompt; shutting down application");
            Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
    }

    private static Window? GetPreferredOwner()
    {
        return Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
               ?? Application.Current?.MainWindow;
    }
}
