using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.Model.Schema;
using ENRLLauncher.MVVM.View.Dialogs;
using ENRLLauncher.MVVM.ViewModel.Dialogs;

namespace ENRLLauncher.MVVM.ViewModel
{

    public class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IJsonStorageService _storageService;
        private readonly ISecurityService _securityService;
        private readonly IAppLogger? _logger;

        private bool _startInFullScreen;
        private bool _launchOnWindowsStartup;
        private bool _requirePinForEditMode;
        private bool _hasUnsavedChanges;
        private bool _isSaving;
        private string _statusMessage = "Ready";
        private string _backupStatusMessage = string.Empty;

        private CancellationTokenSource? _saveDebounceCts;

        // --- Options Properties ---

        public bool StartInFullScreen
        {
            get => _startInFullScreen;
            set
            {
                if (Set(ref _startInFullScreen, value))
                {
                    HasUnsavedChanges = true;
                    StatusMessage = "Unsaved changes";
                }
            }
        }

        public bool LaunchOnWindowsStartup
        {
            get => _launchOnWindowsStartup;
            set
            {
                if (Set(ref _launchOnWindowsStartup, value))
                {
                    HasUnsavedChanges = true;
                    StatusMessage = "Unsaved changes";
                }
            }
        }

        public bool RequirePinForEditMode
        {
            get => _requirePinForEditMode;
            set
            {
                if (_requirePinForEditMode != value)
                {
                    HandlePinToggleRequest(value);
                }
            }
        }

        public bool HasPinConfigured => _securityService.HasPinSet;

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => Set(ref _hasUnsavedChanges, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            set => Set(ref _isSaving, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        public string BackupStatusMessage
        {
            get => _backupStatusMessage;
            set => Set(ref _backupStatusMessage, value);
        }

        // --- Commands ---

        public ICommand SaveSettingsCommand { get; }
        public ICommand ExportLayoutCommand { get; }
        public ICommand ImportLayoutCommand { get; }
        public ICommand RestoreAutoBackupCommand { get; }
        public ICommand OpenAppDataFolderCommand { get; }
        public ICommand OpenLogsFolderCommand { get; }
        public ICommand ChangePinCommand { get; }

        public SettingsViewModel(
            ISettingsService settingsService,
            IFileDialogService fileDialogService,
            IJsonStorageService storageService,
            ISecurityService securityService,
            IAppLogger? logger = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            _logger = logger;

            SaveSettingsCommand = new RelayCommand(_ => TriggerDebouncedSave(), _ => !IsSaving);
            ExportLayoutCommand = new RelayCommand(_ => ExecuteExportLayout());
            ImportLayoutCommand = new RelayCommand(_ => ExecuteImportLayout());
            RestoreAutoBackupCommand = new RelayCommand(_ => ExecuteRestoreAutoBackup());
            OpenAppDataFolderCommand = new RelayCommand(_ => OpenFolder(Globals.g_AppDir));
            OpenLogsFolderCommand = new RelayCommand(_ => OpenFolder(Globals.g_LogsDir));
            ChangePinCommand = new RelayCommand(_ => ExecuteChangePin(), _ => RequirePinForEditMode);

            _ = LoadInitialSettingsAsync();
        }

        // Loads current configuration from disk on initialization
        private async Task LoadInitialSettingsAsync()
        {
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                _startInFullScreen = settings.StartInFullScreen;

                // Check real registry status as primary indicator of startup launch
                var isRegistryStartup = _settingsService.IsWindowsStartupEnabled();
                _launchOnWindowsStartup = isRegistryStartup || settings.LaunchOnWindowsStartup;

                _requirePinForEditMode = _securityService.IsPinLockEnabled;

                OnPropertyChanged(nameof(StartInFullScreen));
                OnPropertyChanged(nameof(LaunchOnWindowsStartup));
                OnPropertyChanged(nameof(RequirePinForEditMode));
                OnPropertyChanged(nameof(HasPinConfigured));
                HasUnsavedChanges = false;
                StatusMessage = "Settings loaded";
            }
            catch (Exception ex)
            {
                _logger?.Error(nameof(SettingsViewModel), "Error loading initial settings", ex);
                StatusMessage = "Error loading settings";
            }
        }

        // Handles requests to enable or disable the edit mode PIN requirement
        private void HandlePinToggleRequest(bool enable)
        {
            if (enable)
            {
                // If a PIN is already configured in the registry, simply enable it
                if (_securityService.HasPinSet)
                {
                    _securityService.SetPinLockEnabled(true);
                    Set(ref _requirePinForEditMode, true);
                    OnPropertyChanged(nameof(HasPinConfigured));
                    StatusMessage = "PIN protection enabled";
                    return;
                }

                // Prompt user to create a new PIN
                var dialogVm = new PinPromptDialogViewModel(_securityService, PinDialogMode.Setup, _logger);
                var dialog = new PinPromptDialog
                {
                    DataContext = dialogVm,
                    Owner = Application.Current?.MainWindow
                };

                dialogVm.RequestClose += () => dialog.Close();
                dialog.ShowDialog();

                if (dialogVm.Success)
                {
                    Set(ref _requirePinForEditMode, true);
                    OnPropertyChanged(nameof(HasPinConfigured));
                    StatusMessage = "PIN configured and enabled";
                }
                else
                {
                    // Revert checkbox state
                    OnPropertyChanged(nameof(RequirePinForEditMode));
                }
            }
            else
            {
                // Disabling requires verification of current PIN or Windows Administrator
                var dialogVm = new PinPromptDialogViewModel(_securityService, PinDialogMode.Verify, _logger);
                var dialog = new PinPromptDialog
                {
                    DataContext = dialogVm,
                    Owner = Application.Current?.MainWindow
                };

                dialogVm.RequestClose += () => dialog.Close();
                dialog.ShowDialog();

                if (dialogVm.Success)
                {
                    _securityService.SetPinLockEnabled(false);
                    Set(ref _requirePinForEditMode, false);
                    OnPropertyChanged(nameof(HasPinConfigured));
                    StatusMessage = "PIN protection disabled";
                }
                else
                {
                    // Revert checkbox state
                    OnPropertyChanged(nameof(RequirePinForEditMode));
                }
            }
        }

        // Prompts user to change or update existing PIN
        private void ExecuteChangePin()
        {
            var dialogVm = new PinPromptDialogViewModel(_securityService, PinDialogMode.Change, _logger);
            var dialog = new PinPromptDialog
            {
                DataContext = dialogVm,
                Owner = Application.Current?.MainWindow
            };

            dialogVm.RequestClose += () => dialog.Close();
            dialog.ShowDialog();

            if (dialogVm.Success)
            {
                if (dialogVm.WasPinCleared)
                {
                    Set(ref _requirePinForEditMode, false);
                    StatusMessage = "PIN removed and disabled";
                }
                else
                {
                    Set(ref _requirePinForEditMode, true);
                    StatusMessage = "PIN updated successfully";
                }
                OnPropertyChanged(nameof(RequirePinForEditMode));
                OnPropertyChanged(nameof(HasPinConfigured));
            }
        }

        // Coalesces save invocations into a debounced save with explicit backup creation
        private void TriggerDebouncedSave()
        {
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
            _saveDebounceCts = new CancellationTokenSource();
            var token = _saveDebounceCts.Token;

            IsSaving = true;
            StatusMessage = "Saving changes…";

            Task.Delay(400, token).ContinueWith(async task =>
            {
                if (task.IsCanceled) return;

                try
                {
                    // 1. Create timestamped legacy backup snapshot
                    await _settingsService.CreateBackupAsync();

                    // 2. Persist settings atomically (automatically generates settings.json.bak)
                    var schema = new SettingsSchema
                    {
                        StartInFullScreen = StartInFullScreen,
                        LaunchOnWindowsStartup = LaunchOnWindowsStartup
                    };
                    await _settingsService.SaveSettingsAsync(schema);

                    // 3. Synchronize Windows startup registry key
                    _settingsService.SetWindowsStartup(LaunchOnWindowsStartup);

                    HasUnsavedChanges = false;
                    StatusMessage = $"Saved at {DateTime.Now:h:mm:ss tt} (Backup created)";
                    _logger?.Info(nameof(SettingsViewModel), "Settings saved and backup created");
                }
                catch (Exception ex)
                {
                    StatusMessage = "Failed to save settings";
                    _logger?.Error(nameof(SettingsViewModel), "Failed saving settings", ex);
                }
                finally
                {
                    IsSaving = false;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // Exports the current layout.json to a destination file chosen by the user
        private void ExecuteExportLayout()
        {
            if (!File.Exists(Globals.g_LayoutPath))
            {
                BackupStatusMessage = "No layout file found to export.";
                return;
            }

            var defaultName = $"ENRLLauncher_Layout_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var destPath = _fileDialogService.SaveFile("Export Layout Configuration", "JSON Files (*.json)|*.json",
                defaultName, ".json");
            if (string.IsNullOrWhiteSpace(destPath)) return;

            try
            {
                File.Copy(Globals.g_LayoutPath, destPath, overwrite: true);
                BackupStatusMessage = $"Layout exported to {Path.GetFileName(destPath)}";
                _logger?.Info(nameof(SettingsViewModel), $"Exported layout to {destPath}");
            }
            catch (Exception ex)
            {
                BackupStatusMessage = "Failed to export layout file.";
                _logger?.Error(nameof(SettingsViewModel), "Error exporting layout", ex);
            }
        }

        // Imports and validates an external layout JSON file
        private async void ExecuteImportLayout()
        {
            var filePath = _fileDialogService.OpenFile("Restore Layout Backup", "JSON Files (*.json)|*.json");
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            try
            {
                var testLoad = await _storageService.LoadAsync<LayoutSchema>(filePath);
                if (testLoad == null || testLoad.Items == null)
                {
                    BackupStatusMessage = "Selected file is not a valid layout backup.";
                    return;
                }

                // Create backup of current layout before overwriting
                if (File.Exists(Globals.g_LayoutPath))
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    var archivePath = Path.Combine(Globals.g_SettingsLegacyDir, $"layout_pre_restore_{timestamp}.json");
                    File.Copy(Globals.g_LayoutPath, archivePath, overwrite: true);
                }

                File.Copy(filePath, Globals.g_LayoutPath, overwrite: true);
                BackupStatusMessage = "Layout restored! Restart or refresh to view changes.";
                _logger?.Info(nameof(SettingsViewModel), $"Imported layout from {filePath}");
            }
            catch (Exception ex)
            {
                BackupStatusMessage = "Error importing layout file.";
                _logger?.Error(nameof(SettingsViewModel), "Failed importing layout", ex);
            }
        }

        // Restores layout from the last automatically generated .bak file
        private void ExecuteRestoreAutoBackup()
        {
            var bakPath = $"{Globals.g_LayoutPath}.bak";
            if (!File.Exists(bakPath))
            {
                BackupStatusMessage = "No automatic backup (.bak) found.";
                return;
            }

            try
            {
                File.Copy(bakPath, Globals.g_LayoutPath, overwrite: true);
                BackupStatusMessage = "Restored from last auto-backup (.bak).";
                _logger?.Info(nameof(SettingsViewModel), "Restored layout from .bak file");
            }
            catch (Exception ex)
            {
                BackupStatusMessage = "Failed restoring from auto-backup.";
                _logger?.Error(nameof(SettingsViewModel), "Error restoring .bak", ex);
            }
        }

        // Opens an explorer window pointing to the given folder path
        private void OpenFolder(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(nameof(SettingsViewModel), $"Failed opening directory {folderPath}", ex);
            }
        }
    }
}
