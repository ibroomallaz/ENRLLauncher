using System.Diagnostics;
using System.IO;
using System.Net.Http;
using ENRLLauncher.Core.Interfaces;

namespace ENRLLauncher.Core.Services;

public sealed class HttpService : IHttpService, IDisposable
{
    private readonly HttpClient _client;
    private bool _disposed;

    public HttpService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("ENRLLauncher/1.0");
        }
    }

    public Task<string> GetStringAsync(string url, CancellationToken ct = default)
        => _client.GetStringAsync(url, ct);

    public Task<Stream> GetStreamAsync(string url, CancellationToken ct = default)
        => _client.GetStreamAsync(url, ct);

    public async Task DownloadFileAsync(string url, string filePath, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = filePath + ".tmp";
        var succeeded = false;

        try
        {
            using var resp = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await src.CopyToAsync(dst, ct);

            if (File.Exists(filePath))
                File.Replace(tmp, filePath, destinationBackupFileName: null);
            else
                File.Move(tmp, filePath);

            succeeded = true;
        }
        catch
        {
            if (File.Exists(tmp)) File.Delete(tmp);
            throw;
        }
        finally
        {
            if (!succeeded && File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public bool TryOpenUrl(string target, out Exception? error)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _client.Dispose();
        _disposed = true;
    }
}
