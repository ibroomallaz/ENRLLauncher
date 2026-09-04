using System.IO;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;
using ENRLLauncher.MVVM.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ENRLLauncher.Core.Services;

public class JsonStorageService : IJsonStorageService
{
    private readonly JsonSerializerSettings _serializerSettings;

    public JsonStorageService()
    {
        _serializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        _serializerSettings.Converters.Add(new StringEnumConverter());
    }

    public bool Exists(string filePath) => File.Exists(filePath);

    public async Task<T?> LoadAsync<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
        {
            // Try fallback to .bak if primary is missing
            var fallbackPath = $"{filePath}.bak";
            if (File.Exists(fallbackPath))
            {
                return await TryReadFileAsync<T>(fallbackPath);
            }
            return null;
        }

        var result = await TryReadFileAsync<T>(filePath);
        if (result != null) return result;

        // Primary failed/corrupt: archive it and try .bak
        ArchiveCorruptFile(filePath);

        var backup = $"{filePath}.bak";
        if (File.Exists(backup))
        {
            return await TryReadFileAsync<T>(backup);
        }

        return null;
    }

    private async Task<T?> TryReadFileAsync<T>(string path) where T : class
    {
        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonConvert.DeserializeObject<T>(json, _serializerSettings);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync<T>(string filePath, T data, bool atomic = true) where T : class
    {
        StorageBootstrapper.EnsureCoreDirs();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonConvert.SerializeObject(data, _serializerSettings);

        if (!atomic)
        {
            await File.WriteAllTextAsync(filePath, json);
            return;
        }

        var tempPath = $"{filePath}.tmp";
        var backupPath = $"{filePath}.bak";

        await File.WriteAllTextAsync(tempPath, json);

        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, filePath);
        }
    }

    private static void ArchiveCorruptFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var destPath = Path.Combine(Globals.g_SettingsLegacyDir, $"{fileName}_corrupt_{timestamp}.json");
                File.Move(filePath, destPath);
            }
        }
        catch
        {
            // Suppress fallback exceptions so the app can continue starting
        }
    }
}