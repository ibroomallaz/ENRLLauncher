using Newtonsoft.Json;

namespace ENRLLauncher.MVVM.Model.Schema;

public class LayoutSchema : MetaBase
{
    [JsonProperty("schemaVersion")]
    public override int SchemaVersion { get; set; } = Globals.g_LayoutSchema;

    [JsonProperty("items")]
    public List<LaunchItem> Items { get; set; } = [];
}