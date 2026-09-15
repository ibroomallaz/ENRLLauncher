using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ENRLLauncher.Core.Enums;

namespace ENRLLauncher.MVVM.Model.Schema;

public class SettingsSchema : MetaBase
{
    [JsonProperty("schemaVersion")]
    public override int SchemaVersion { get; set; } = Globals.g_SettingsSchema;

    // When true, the launcher opens directly in fullscreen presentation mode
    [JsonProperty("startInFullScreen")]
    public bool StartInFullScreen { get; set; }

    // When true, launches the app automatically when Windows boots / user logs in
    [JsonProperty("launchOnWindowsStartup")]
    public bool LaunchOnWindowsStartup { get; set; }

    // Minimum severity level required for file logging
    [JsonProperty("logLevel")]
    [JsonConverter(typeof(StringEnumConverter))]
    public AppLogLevel LogLevel { get; set; } = AppLogLevel.Info;

    // Reserved for future custom background image path
    [JsonProperty("customBackgroundPath", NullValueHandling = NullValueHandling.Ignore)]
    public string? CustomBackgroundPath { get; set; }
}
