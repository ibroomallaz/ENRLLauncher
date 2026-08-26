using ENRLLauncher.Core.Utilities;
using System.IO;

namespace ENRLLauncher.MVVM.Model
{
    public class Globals
    {
#pragma warning disable CA2211 // Non-constant fields should not be visible
        public static string g_AppVersion = VersionDisplayHelper.GetSemVerDisplay();
        public static string g_FileVersion = VersionDisplayHelper.GetFileVersionDisplay();
#pragma warning restore CA2211 // Non-constant fields should not be visible
        // Application data directories (local)
        public static readonly string g_AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "UArizona", "ENRLLauncher");
        public static readonly string g_DataDir = Path.Combine(g_AppDir, "data");

        // Settings file (local)
        public const string g_SettingsFileName = "settings.json";
        public static readonly string g_SettingsPath = Path.Combine(g_AppDir, g_SettingsFileName);

        // Backup/cache files (local)
        public static readonly string g_DepartmentCachePath = Path.Combine(g_DataDir, "departments.json");
        public static readonly string g_LinksCachePath = Path.Combine(g_DataDir, "links.json");

        // Logs + legacy settings dirs
        public static readonly string g_LogsDir = Path.Combine(g_AppDir, "logs");
        public static readonly string g_SettingsLegacyDir = Path.Combine(g_AppDir, "settings-legacy");

        // Schema versions
        public const int g_SettingsSchema = 3;
        public const int g_DepartmentJSONSchema = 3;
        public const int g_LinkJSONSchema = 2;
        public const int g_VersionSchema = 3;
    }
}