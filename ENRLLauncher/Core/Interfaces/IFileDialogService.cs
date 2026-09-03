namespace ENRLLauncher.Core.Interfaces;

public interface IFileDialogService
{
    string? OpenFile(string title, string filter, string? initialDirectory = null);
    string[]? OpenFiles(string title, string filter, string? initialDirectory = null);
    string? SaveFile(string title, string filter, string? defaultFileName = null, string? defaultExtension = null, string? initialDirectory = null);
}