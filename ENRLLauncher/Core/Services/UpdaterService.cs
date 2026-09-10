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
        if (!Directory.Exists(runtimePath))
        {
            _logger?.Write(AppLogLevel.Warning, "UpdaterService", $"Runtime path '{runtimePath}' not found.");
            return false;
        }

        var found = Directory.GetDirectories(runtimePath)
            .Select(Path.GetFileName)
            .Any(v => Version.TryParse(v, out var installed) && installed >= required);

        _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Probed .NET Desktop Runtime required={required}, installed={found}");
        return found;
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
            _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Starting background download to: {tempDir}, needsFramework={needsFramework}");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(90);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ENRLLauncher/1.0");

            if (needsFramework && !string.IsNullOrWhiteSpace(updateInfo.SetupUrl) && !string.IsNullOrWhiteSpace(updateInfo.MsiUrl))
            {
                progressReporter?.Report("Downloading .NET bootstrapper…");
                _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Downloading bootstrapper from {updateInfo.SetupUrl}");
                await DownloadFileAsync(client, updateInfo.SetupUrl, setupPath);

                progressReporter?.Report("Downloading update…");
                _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Downloading update MSI from {updateInfo.MsiUrl}");
                await DownloadFileAsync(client, updateInfo.MsiUrl, msiPath);

                progressReporter?.Report("Update ready. App will restart shortly…");
                await Task.Delay(1500);

                _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Executing setup bootstrapper: {setupPath}");
                ExecuteInstaller(setupPath, "/quiet /norestart");
            }
            else if (!string.IsNullOrWhiteSpace(updateInfo.MsiUrl))
            {
                progressReporter?.Report("Downloading update…");
                _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Downloading update MSI from {updateInfo.MsiUrl}");
                await DownloadFileAsync(client, updateInfo.MsiUrl, msiPath);

                progressReporter?.Report("Update ready. App will restart shortly…");
                await Task.Delay(1500);

                _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Executing msiexec for: {msiPath}");
                ExecuteInstaller("msiexec.exe", $"/i \"{msiPath}\" /qn /norestart");
            }
            else
            {
                throw new InvalidOperationException("No valid download URLs provided in update manifest.");
            }

            _logger?.Write(AppLogLevel.Info, "UpdaterService", "Update process launched, shutting down application");
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

    private static void ExecuteInstaller(string fileName, string arguments)
    {
        var appPath = Process.GetCurrentProcess().MainModule?.FileName;

        if (!string.IsNullOrEmpty(appPath))
        {
            var psCommand = $"-Command \"Start-Process '{fileName}' -ArgumentList '{arguments}' -Wait; Start-Process '{appPath}' -ArgumentList '-updated'\"";

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
            _logger?.Write(AppLogLevel.Info, "UpdaterService", $"User elected manual download: {fallbackUrl}");
            if (_httpService != null)
            {
                _httpService.TryOpenUrl(fallbackUrl, out _);
            }
            else
            {
                try { Process.Start(new ProcessStartInfo(fallbackUrl) { UseShellExecute = true }); } catch { /* ignored */ }
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
                var cleaned = 0;
                foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), "ENRL_Update_*"))
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                        cleaned++;
                    }
                    catch { /* directory might still be locked */ }
                }

                if (cleaned > 0)
                {
                    _logger?.Write(AppLogLevel.Info, "UpdaterService", $"Cleaned {cleaned} old update temp directories");
                }
            }
            catch (Exception ex)
            {
                _logger?.Write(AppLogLevel.Warning, "UpdaterService", $"Cleanup old updates warning: {ex.Message}");
            }
        });
    }
}
