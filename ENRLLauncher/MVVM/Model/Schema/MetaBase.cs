using Newtonsoft.Json;

namespace ENRLLauncher.MVVM.Model.Schema;

public abstract class MetaBase
{
    [JsonProperty("schemaVersion")]
    public virtual int SchemaVersion { get; set; }

    [JsonProperty("appVersion")]
    public string AppVersion { get; set; } = Globals.g_AppVersion;

    [JsonProperty("lastModifiedUtc")]
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
}