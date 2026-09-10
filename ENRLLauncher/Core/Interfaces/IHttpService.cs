using System.IO;

namespace ENRLLauncher.Core.Interfaces;

public interface IHttpService
{
    Task<string> GetStringAsync(string url, CancellationToken ct = default);
    Task<Stream> GetStreamAsync(string url, CancellationToken ct = default);
    Task DownloadFileAsync(string url, string filePath, CancellationToken ct = default);
    bool TryOpenUrl(string target, out Exception? error);
}
