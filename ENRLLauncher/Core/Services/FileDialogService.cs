using Microsoft.Win32;
using ENRLLauncher.Core.Interfaces;

namespace ENRLLauncher.Core.Services;

public class FileDialogService : IFileDialogService
{
    public string? OpenFile(string title, string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = false,
            InitialDirectory = initialDirectory ?? string.Empty
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string[]? OpenFiles(string title, string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = true,
            InitialDirectory = initialDirectory ?? string.Empty
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : null;
    }

    public string? SaveFile(string title, string filter, string? defaultFileName = null, string? defaultExtension = null, string? initialDirectory = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName ?? string.Empty,
            DefaultExt = defaultExtension ?? string.Empty,
            AddExtension = true,
            InitialDirectory = initialDirectory ?? string.Empty,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}