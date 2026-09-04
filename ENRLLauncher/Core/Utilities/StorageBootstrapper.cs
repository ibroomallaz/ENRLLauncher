using System.IO;
using ENRLLauncher.MVVM.Model;

namespace ENRLLauncher.Core.Utilities;

public static class StorageBootstrapper
{
    public static void EnsureCoreDirs()
    {
        EnsureDirSafe(Globals.g_AppDir);
        EnsureDirSafe(Globals.g_LogsDir);
        EnsureDirSafe(Globals.g_SettingsLegacyDir);
        EnsureDirSafe(Globals.g_DataDir);
    }

    public static bool TryEnsureCoreDirs(out string? error)
    {
        try
        {
            EnsureCoreDirs();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void EnsureDirSafe(string path)
    {
        if (File.Exists(path))
        {
            throw new IOException($"A file exists where a directory is expected: {path}");
        }

        Directory.CreateDirectory(path);
    }
}