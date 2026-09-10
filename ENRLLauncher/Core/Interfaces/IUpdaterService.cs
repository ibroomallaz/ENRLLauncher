using ENRLLauncher.MVVM.Model.Schema;

namespace ENRLLauncher.Core.Interfaces;

public interface IUpdaterService
{
    // Checks if the specified .NET Desktop runtime version is installed on the local system
    bool IsTargetRuntimePresent(string? requiredVersion);

    // Downloads the update files to a temporary workspace and runs the installer
    Task DownloadAndInstallAsync(CurrentVersion updateInfo, IProgress<string>? progressReporter = null);

    // Asynchronously cleans up previous ENRL_Update_* temporary folders
    Task CleanupOldUpdatesAsync();
}
