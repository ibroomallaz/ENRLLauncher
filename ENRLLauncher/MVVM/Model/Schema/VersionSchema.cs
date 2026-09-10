using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace ENRLLauncher.MVVM.Model.Schema;

public sealed class VersionManifest : MetaBase
{
    public VersionManifest()
    {
        SchemaVersion = Globals.g_VersionSchema;
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        if (SchemaVersion < Globals.g_VersionSchema)
        {
            SchemaVersion = Globals.g_VersionSchema;
        }
    }

    [JsonProperty("$schema", NullValueHandling = NullValueHandling.Ignore)]
    public string? Schema { get; set; }

    [JsonProperty(nameof(Version), NullValueHandling = NullValueHandling.Ignore)]
    public VersionInfo? Version { get; set; }

    [JsonProperty(nameof(Required), NullValueHandling = NullValueHandling.Ignore)]
    public RequiredUpdate? Required { get; set; }
}

public sealed class VersionInfo
{
    [JsonProperty(nameof(Current))]
    public CurrentVersion? Current { get; set; }

    [JsonProperty(nameof(PreRelease))]
    public PreReleaseVersion? PreRelease { get; set; }
}

public sealed class CurrentVersion
{
    [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
    public string? Version { get; set; }

    [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
    public string? Location { get; set; }

    [JsonProperty("msiUrl", NullValueHandling = NullValueHandling.Ignore)]
    public string? MsiUrl { get; set; }

    [JsonProperty("setupUrl", NullValueHandling = NullValueHandling.Ignore)]
    public string? SetupUrl { get; set; }

    [JsonProperty("requiredDotNetVersion", NullValueHandling = NullValueHandling.Ignore)]
    public string? RequiredDotNetVersion { get; set; }

    [JsonProperty("changelog", NullValueHandling = NullValueHandling.Ignore)]
    public string? Changelog { get; set; }
}

public sealed class PreReleaseVersion
{
    [JsonProperty("exists")]
    public bool Exists { get; set; }

    [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
    public string? Version { get; set; }

    [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
    public string? Location { get; set; }

    [JsonProperty("msiUrl", NullValueHandling = NullValueHandling.Ignore)]
    public string? MsiUrl { get; set; }

    [JsonProperty("setupUrl", NullValueHandling = NullValueHandling.Ignore)]
    public string? SetupUrl { get; set; }

    [JsonProperty("requiredDotNetVersion", NullValueHandling = NullValueHandling.Ignore)]
    public string? RequiredDotNetVersion { get; set; }

    [JsonProperty("changelog", NullValueHandling = NullValueHandling.Ignore)]
    public string? Changelog { get; set; }
}

public sealed class RequiredUpdate
{
    [JsonProperty("minVersion", NullValueHandling = NullValueHandling.Ignore)]
    public string? MinVersion { get; set; }

    [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; set; }
}
