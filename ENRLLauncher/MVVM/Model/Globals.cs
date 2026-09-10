using ENRLLauncher.Core.Utilities;
using System.IO;

namespace ENRLLauncher.MVVM.Model
{
    public static class Globals
    {
#pragma warning disable CA2211 // Non-constant fields should not be visible
        public static readonly string g_AppVersion = VersionDisplayHelper.GetSemVerDisplay();
        public static string g_FileVersion = VersionDisplayHelper.GetFileVersionDisplay();
#pragma warning restore CA2211 // Non-constant fields should not be visible

        // Remote Box static JSON locations
        public const string g_VersionJSON = "https://arizona.box.com/shared/static/placeholder_version.json";
        public const string g_TestVersionJSON = "https://arizona.box.com/shared/static/placeholder_test_version.json";

        // Application data directories (local)
        public static readonly string g_AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "UArizona", "ENRLLauncher");
        public static readonly string g_DataDir = Path.Combine(g_AppDir, "data");

        // Settings file (local)
        private const string g_SettingsFileName = "settings.json";
        public static readonly string g_SettingsPath = Path.Combine(g_AppDir, g_SettingsFileName);

        // Layout persistence file (local)
        private const string g_LayoutFileName = "layout.json";
        public static readonly string g_LayoutPath = Path.Combine(g_AppDir, g_LayoutFileName);

        // Logs + legacy settings dirs
        public static readonly string g_LogsDir = Path.Combine(g_AppDir, "logs");
        public static readonly string g_SettingsLegacyDir = Path.Combine(g_AppDir, "settings-legacy");

        // Schema versions
        public const int g_SettingsSchema = 1;
        public const int g_VersionSchema = 1;
        public const int g_LayoutSchema = 1;
    }
}
