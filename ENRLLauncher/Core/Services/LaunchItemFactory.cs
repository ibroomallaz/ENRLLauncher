using System.IO;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.MVVM.Model;

namespace ENRLLauncher.Core.Services;

public class LaunchItemFactory
{
    public static LaunchItem CreateFromFilePath(string path, int currentCount)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();

        var targetType = extension switch
        {
            ".pptx" or ".ppt" or ".ppsx" or ".pps" or ".pptm" => LaunchTargetType.Presentation,
            ".exe" or ".bat" or ".cmd" or ".ps1" => LaunchTargetType.Application,
            ".pdf" or ".docx" or ".xlsx" or ".txt" => LaunchTargetType.Document,
            _ => LaunchTargetType.Document
        };

        return new LaunchItem
        {
            Title = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(path) : fileName,
            Description = extension.TrimStart('.').ToUpperInvariant(),
            TargetPath = path,
            TargetType = targetType,
            SortOrder = currentCount
        };
    }
}