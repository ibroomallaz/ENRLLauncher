using System.Diagnostics;
using System.IO;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.MVVM.Model;

namespace ENRLLauncher.Core.Services;

public class LauncherService : ILauncherService
{
    public bool CanLaunch(LaunchItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.TargetPath))
        {
            return false;
        }

        return item.TargetType switch
        {
            LaunchTargetType.WebLink => Uri.TryCreate(item.TargetPath, UriKind.Absolute, out var uri)
                                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
            LaunchTargetType.Presentation or
            LaunchTargetType.Application or
            LaunchTargetType.Document => File.Exists(item.TargetPath) || Directory.Exists(item.TargetPath),
            _ => false
        };
    }

    public Task<bool> LaunchAsync(LaunchItem item)
    {
        return Task.Run(() =>
        {
            if (!CanLaunch(item))
            {
                return false;
            }

            try
            {
                var psi = CreateStartInfo(item);
                using var process = Process.Start(psi);
                return process != null;
            }
            catch (Exception)
            {
                return false;
            }
        });
    }

    private ProcessStartInfo CreateStartInfo(LaunchItem item)
    {
        var psi = new ProcessStartInfo
        {
            FileName = item.TargetPath,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(item.Arguments))
        {
            psi.Arguments = item.Arguments;
            return psi;
        }

        switch (item.TargetType)
        {
            case LaunchTargetType.Presentation:
                var ext = Path.GetExtension(item.TargetPath);

                // Apply /s only to standard editable decks so PowerPoint forces presentation mode
                if (ext.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".ppt", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".pptm", StringComparison.OrdinalIgnoreCase))
                {
                    psi.Arguments = $"/s \"{item.TargetPath}\"";
                }
                break;

            case LaunchTargetType.Application:
                if (Path.IsPathRooted(item.TargetPath))
                {
                    psi.WorkingDirectory = Path.GetDirectoryName(item.TargetPath);
                }
                break;

            case LaunchTargetType.WebLink:
            case LaunchTargetType.Document:
            default:
                break;
        }

        return psi;
    }
}