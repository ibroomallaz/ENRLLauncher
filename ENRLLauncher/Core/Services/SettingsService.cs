using System.IO;
using Microsoft.Win32;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.Model.Schema;

namespace ENRLLauncher.Core.Services
{

    public class SettingsService : ISettingsService
    {
        private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryValueName = "ENRLLauncher";

        private readonly IJsonStorageService _storageService;
        private readonly IAppLogger? _logger;

        private CancellationTokenSource? _debounceCts;
        private SettingsSchema? _pendingSettings;
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        public SettingsService(IJsonStorageService storageService, IAppLogger? logger = null)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _logger = logger;
        }

        // Loads settings from disk or returns default configuration
        public async Task<SettingsSchema> LoadSettingsAsync()
        {
            if (!_storageService.Exists(Globals.g_SettingsPath))
            {
                _logger?.Info(nameof(SettingsService), "Settings file not found, creating default settings");
                return new SettingsSchema();
            }

            try
            {
                var loaded = await _storageService.LoadAsync<SettingsSchema>(Globals.g_SettingsPath);
                return loaded ?? new SettingsSchema();
            }
            catch (Exception ex)
            {
                _logger?.Error(nameof(SettingsService), $"Failed to load settings from {Globals.g_SettingsPath}", ex);
                return new SettingsSchema();
            }
        }

        // Immediately writes settings to disk atomically, generating settings.json.bak
        public async Task SaveSettingsAsync(SettingsSchema settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            await _saveLock.WaitAsync();
            try
            {
                settings.LastModifiedUtc = DateTime.UtcNow;
                await _storageService.SaveAsync(Globals.g_SettingsPath, settings, atomic: true);
                _logger?.Info(nameof(SettingsService), "Settings saved atomically with .bak backup");
            }
            catch (Exception ex)
            {
                _logger?.Error(nameof(SettingsService), "Failed saving settings", ex);
                throw;
            }
            finally
            {
                _saveLock.Release();
            }
        }

        // Debounces save requests to reduce unnecessary disk writes
        public void RequestSave(SettingsSchema settings, int debounceMs = 1000)
        {
            ArgumentNullException.ThrowIfNull(settings);

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            _pendingSettings = settings;

            var token = _debounceCts.Token;

            Task.Delay(debounceMs, token).ContinueWith(async task =>
            {
                if (!task.IsCanceled && _pendingSettings != null)
                {
                    await SaveSettingsAsync(_pendingSettings);
                }
            }, TaskScheduler.Default);
        }

        // Flushes any pending debounced save immediately
        public async Task FlushSaveAsync()
        {
            if (_debounceCts is { IsCancellationRequested: false } && _pendingSettings != null)
            {
                _debounceCts.Cancel();
                await SaveSettingsAsync(_pendingSettings);
                _pendingSettings = null;
            }
        }

        // Creates an explicit timestamped backup file in the legacy settings directory
        public async Task<string?> CreateBackupAsync()
        {
            if (!File.Exists(Globals.g_SettingsPath))
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(Globals.g_SettingsLegacyDir);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(Globals.g_SettingsLegacyDir, $"settings_backup_{timestamp}.json");

                await using var sourceStream = File.OpenRead(Globals.g_SettingsPath);
                await using var destStream = File.Create(backupPath);
                await sourceStream.CopyToAsync(destStream);

                _logger?.Info(nameof(SettingsService), $"Created timestamped settings backup at {backupPath}");
                return backupPath;
            }
            catch (Exception ex)
            {
                _logger?.Error(nameof(SettingsService), "Failed to create settings backup", ex);
                return null;
            }
        }

        // Toggles Windows startup via the CurrentUser Run registry key
        public bool SetWindowsStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(StartupRegistryKey, writable: true);

                if (enable)
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppRegistryValueName, $"\"{exePath}\"");
                        _logger?.Info(nameof(SettingsService), "Enabled Windows startup registry key");
                        return true;
                    }

                    return false;
                }
                else
                {
                    if (key.GetValue(AppRegistryValueName) != null)
                    {
                        key.DeleteValue(AppRegistryValueName, false);
                        _logger?.Info(nameof(SettingsService), "Removed Windows startup registry key");
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(nameof(SettingsService), "Failed updating Windows startup registry key", ex);
                return false;
            }
        }

        // Checks if the app is currently registered in the Windows startup registry
        public bool IsWindowsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: false);
                return key?.GetValue(AppRegistryValueName) != null;
            }
            catch (Exception ex)
            {
                _logger?.Warn(nameof(SettingsService), $"Unable to query startup registry: {ex.Message}");
                return false;
            }
        }
    }
}