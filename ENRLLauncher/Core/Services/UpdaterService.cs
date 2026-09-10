using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.MVVM.Model.Schema;

namespace ENRLLauncher.Core.Services;

public class UpdaterService : IUpdaterService
{
    private readonly IAppLogger? _logger;
    private readonly IHttpService? _httpService;

    public UpdaterService(IAppLogger? logger = null, IHttpService? httpService = null)
    {
        _logger = logger;
        _httpService = httpService;
    }

    public bool IsTargetRuntimePresent(string? requiredVersion)
    {
        if (string.IsNullOrWhiteSpace(requiredVersion)) return true;
        if (!Version.TryParse(requiredVersion, out var required)) return true;

        const string runtimePath = @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App";
        if (!Directory.Exists(runtimePath)) return false;

        return Directory.GetDirectories(runtimePath)
            .Select(Path.GetFileName)
            .Any(v => Version.TryParse(v, out var installed) && installed >= required);
    }

    public async Task DownloadAndInstallAsync(CurrentVersion updateInfo, IProgress<string>? progressReporter = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ENRL_Update_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var msiPath = Path.Combine(tempDir, "ENRL_Installer.msi");
        var setupPath = Path.Combine(tempDir, "setup.exe");

        var needsFramework = !string.IsNullOrWhiteSpace(updateInfo.RequiredDotNetVersion) &&
                             !IsTargetRuntimePresent(updateInfo.RequiredDotNetVersion);

        try
        {
            _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Starting background download to: {tempDir}");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ENRLLauncher/1.0");

            if (needsFramework && !string.IsNullOrWhiteSpace(updateInfo.SetupUrl) && !string.IsNullOrWhiteSpace(updateInfo.MsiUrl))
            {
                progressReporter?.Report("Downloading .NET bootstrapper…");
                await DownloadFileAsync(client, updateInfo.SetupUrl, setupPath);

                progressReporter?.Report("Downloading update…");
                await DownloadFileAsync(client, updateInfo.MsiUrl, msiPath);

                progressReporter?.Report("Update ready. App will restart shortly…");
                await Task.Delay(1500);

                ExecuteInstaller(setupPath, "/quiet /norestart");
            }
            else if (!string.IsNullOrWhiteSpace(updateInfo.MsiUrl))
            {
                progressReporter?.Report("Downloading update…");
                await DownloadFileAsync(client, updateInfo.MsiUrl, msiPath);

                progressReporter?.Report("Update ready. App will restart shortly…");
                await Task.Delay(1500);

                ExecuteInstaller("msiexec.exe", $"/i \"{msiPath}\" /qn /norestart");
            }
            else
            {
                throw new InvalidOperationException("No valid download URLs provided in update manifest.");
            }

            // Shutdown current instance so the installer can overwrite files
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Error, "UpdaterService", $"Update failed: {ex.Message}");
            HandleFailure(ex.Message, updateInfo.Location, tempDir);
        }
        finally
        {
            progressReporter?.Report("Ready");
        }
    }

    // --- ATOMIC DOWNLOAD PATTERN ---
    // Downloads as .download and renames only after successful completion
    private static async Task DownloadFileAsync(HttpClient client, string url, string destinationPath)
    {
        var tempPath = destinationPath + ".download";
        using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    // --- POWERSHELL WATCHDOG ---
    // Launches the installer and waits for it to exit before restarting this app
    private static void ExecuteInstaller(string fileName, string arguments)
    {
        string? appPath = Process.GetCurrentProcess().MainModule?.FileName;

        if (!string.IsNullOrEmpty(appPath))
        {
            // PowerShell waits for the installer (-Wait), then restarts the original appPath
            string psCommand = $"-Command \"Start-Process '{fileName}' -ArgumentList '{arguments}' -Wait; Start-Process '{appPath}' -ArgumentList '-updated'\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = psCommand,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true
            });
        }
        else
        {
            // Fallback if we can't determine current process path
            Process.Start(new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = true });
        }
    }

    private void HandleFailure(string userMessage, string? fallbackUrl, string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch { /* ignored */ }

        var result = MessageBox.Show(
            $"{userMessage}\n\nDownload manually?",
            "Update Failed",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(fallbackUrl))
        {
            if (_httpService != null)
            {
                _httpService.TryOpenUrl(fallbackUrl, out _);
            }
            else
            {
                try { Process.Start(new ProcessStartInfo(fallbackUrl) { UseShellExecute = true }); } catch { }
            }
        }
    }

    public async Task CleanupOldUpdatesAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(60));
        await Task.Run(() =>
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), "ENRL_Update_*"))
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                    catch { /* directory might still be locked by running process */ }
                }
            }
            catch (Exception ex)
            {
                _logger?.Write(AppLogLevel.Warning, "UpdaterService", $"Cleanup old updates warning: {ex.Message}");
            }
        });
    }
}
