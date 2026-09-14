using ENRLLauncher.MVVM.Model.Schema;

namespace ENRLLauncher.Core.Interfaces
{

    public interface ISettingsService
    {
        // Loads persisted settings from disk, or returns default settings if none exist
        Task<SettingsSchema> LoadSettingsAsync();

        // Immediately writes settings to disk atomically, creating an automatic .bak backup
        Task SaveSettingsAsync(SettingsSchema settings);

        // Debounces save requests to coalesce rapid changes before persisting to disk
        void RequestSave(SettingsSchema settings, int debounceMs = 1000);

        // Flushes any queued debounced save immediately
        Task FlushSaveAsync();

        // Creates an explicit timestamped backup of the current settings file
        Task<string?> CreateBackupAsync();

        // Registers or unregisters the application from Windows startup (HKCU Run key)
        bool SetWindowsStartup(bool enable);

        // Checks whether the application is currently registered to launch on Windows startup
        bool IsWindowsStartupEnabled();
    }
}