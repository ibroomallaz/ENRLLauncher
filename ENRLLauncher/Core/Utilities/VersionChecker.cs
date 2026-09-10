using System.Text.RegularExpressions;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.MVVM.Model.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ENRLLauncher.Core.Utilities;

public class VersionCheckResult
{
    public VersionInfo? Info { get; set; }

    public string? StableVersion { get; set; }
    public string? StableLocation { get; set; }
    public string? StableChangelog { get; set; }
    public string? StableMsiUrl { get; set; }
    public string? StableSetupUrl { get; set; }
    public string? StableRequiredDotNetVersion { get; set; }

    public bool PreExists { get; set; }
    public string? PreVersion { get; set; }
    public string? PreLocation { get; set; }
    public string? PreChangelog { get; set; }
    public string? PreMsiUrl { get; set; }
    public string? PreSetupUrl { get; set; }
    public string? PreRequiredDotNetVersion { get; set; }

    public string? RequiredMinVersion { get; set; }
    public string? RequiredMessage { get; set; }

    public string? Error { get; set; }

    public bool Success => string.IsNullOrEmpty(Error);
    public bool HasAnyStable => !string.IsNullOrWhiteSpace(StableVersion);
    public bool HasAnyPre => !string.IsNullOrWhiteSpace(PreVersion) || PreExists;
}

public static partial class VersionChecker
{
    private const string Cat = "VersionChecker";

    public static async Task<VersionCheckResult> CheckVersionAsync(string url, IHttpService http, IAppLogger? logger = null)
    {
        try
        {
            logger?.Write(AppLogLevel.Info, Cat, $"fetch.start url=\"{url}\"");
            var json = await http.GetStringAsync(url);
            if (string.IsNullOrWhiteSpace(json))
            {
                logger?.Write(AppLogLevel.Warning, Cat, "fetch.empty");
                return new VersionCheckResult { Error = "Empty version response." };
            }

            logger?.Write(AppLogLevel.Info, Cat, $"fetch.ok bytes={json.Length}");

            var res = ExtractRawFields(json);

            VersionInfo? info = null;
            try
            {
                var manifest = JsonConvert.DeserializeObject<VersionManifest>(json);
                if (manifest != null)
                {
                    info = manifest.Version;
                    if (!string.IsNullOrWhiteSpace(manifest.Required?.MinVersion))
                        res.RequiredMinVersion = manifest.Required.MinVersion;
                    if (!string.IsNullOrWhiteSpace(manifest.Required?.Message))
                        res.RequiredMessage = manifest.Required.Message;
                }
            }
            catch { /* ignore */ }

            if (info == null)
            {
                try { info = JsonConvert.DeserializeObject<VersionInfo>(json); } catch { /* ignore */ }
            }

            if (info != null)
            {
                var stable = info.Current?.Version ?? "(none)";
                var pre = info.PreRelease?.Version ?? "(none)";
                var preExists = info.PreRelease?.Exists ?? false;
                logger?.Write(AppLogLevel.Info, Cat, $"parse.ok stable=\"{stable}\" pre=\"{pre}\" pre.exists={preExists}");
            }
            else
            {
                logger?.Write(AppLogLevel.Warning, Cat, "parse.fail");
            }

            res.Info = info;
            return res;
        }
        catch (Exception ex)
        {
            logger?.Write(AppLogLevel.Error, Cat, $"error msg=\"{ex.Message}\"");
            return new VersionCheckResult { Error = ex.Message };
        }
    }

    public static bool IsNewerVersion(string? currentVersion, string? newVersion)
    {
        if (string.IsNullOrWhiteSpace(newVersion)) return false;
        if (string.IsNullOrWhiteSpace(currentVersion)) return true;

        var (baseCurr, labelCurr, numCurr) = ExtractPrerelease(currentVersion);
        var (baseNew, labelNew, numNew) = ExtractPrerelease(newVersion);

        var baseCurrOk = Version.TryParse(baseCurr, out var vCurr);
        var baseNewOk = Version.TryParse(baseNew, out var vNew);

        if (baseCurrOk && baseNewOk)
        {
            if (vNew > vCurr) return true;
            if (vNew < vCurr) return false;

            var currIsPre = !string.IsNullOrEmpty(labelCurr);
            var newIsPre = !string.IsNullOrEmpty(labelNew);

            if (currIsPre && !newIsPre) return true;
            if (!currIsPre && newIsPre) return false;
            if (!currIsPre && !newIsPre) return false;
        }

        if (!string.IsNullOrEmpty(labelCurr) && !string.IsNullOrEmpty(labelNew))
        {
            if (labelCurr.Equals(labelNew, StringComparison.OrdinalIgnoreCase))
            {
                return numNew > numCurr;
            }

            static int GetPriority(string? label) => label?.ToLowerInvariant() switch
            {
                "alpha" => 1,
                "beta" => 2,
                "rc" => 3,
                _ => 0
            };

            var pCurr = GetPriority(labelCurr);
            var pNew = GetPriority(labelNew);

            if (pNew > pCurr) return true;
            if (pNew < pCurr) return false;

            return numNew > numCurr;
        }

        if (Version.TryParse(currentVersion, out var vc) && Version.TryParse(newVersion, out var vn))
        {
            return vn > vc;
        }

        return string.CompareOrdinal(newVersion, currentVersion) > 0;
    }

    private static (string Base, string? Label, int Number) ExtractPrerelease(string version)
    {
        var match = VersionRegex().Match(version.Trim());
        if (!match.Success)
        {
            return (version, null, 1);
        }

        var baseVersion = match.Groups["base"].Value;
        var label = match.Groups["label"].Success ? match.Groups["label"].Value : null;
        var number = int.TryParse(match.Groups["number"].Value, out var n) ? n : 1;
        return (baseVersion, label, number);
    }

    private static VersionCheckResult ExtractRawFields(string json)
    {
        var r = new VersionCheckResult();
        try
        {
            var token = JToken.Parse(json);
            var root = token as JObject ?? [];

            var versionObj = Find(root, "Version") as JObject ?? root;
            var current = Find(versionObj, "Current") as JObject;
            var pre = Find(versionObj, "PreRelease") as JObject;
            var req = Find(root, "Required") as JObject ?? Find(versionObj, "Required") as JObject;

            r.StableVersion = ReadString(current, "version");
            r.StableLocation = ReadString(current, "location");
            r.StableChangelog = ReadString(current, "changelog");
            r.StableMsiUrl = ReadString(current, "msiUrl");
            r.StableSetupUrl = ReadString(current, "setupUrl");
            r.StableRequiredDotNetVersion = ReadString(current, "requiredDotNetVersion");

            r.PreExists = ReadBool(pre, "exists");
            r.PreVersion = ReadString(pre, "version");
            r.PreLocation = ReadString(pre, "location");
            r.PreChangelog = ReadString(pre, "changelog");
            r.PreMsiUrl = ReadString(pre, "msiUrl");
            r.PreSetupUrl = ReadString(pre, "setupUrl");
            r.PreRequiredDotNetVersion = ReadString(pre, "requiredDotNetVersion");

            r.RequiredMinVersion = ReadString(req, "minVersion");
            r.RequiredMessage = ReadString(req, "message");
        }
        catch (Exception ex)
        {
            r.Error = $"JSON read error: {ex.Message}";
        }
        return r;
    }

    private static JToken? Find(JObject obj, string name)
    {
        foreach (var p in obj.Properties())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        }
        return null;
    }

    private static string ReadString(JObject? obj, string name)
    {
        if (obj == null) return string.Empty;
        foreach (var p in obj.Properties())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value.ToString();
        }
        return string.Empty;
    }

    private static bool ReadBool(JObject? obj, string name)
    {
        if (obj == null) return false;
        foreach (var p in obj.Properties())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value.Type == JTokenType.Boolean && p.Value.Value<bool>();
        }
        return false;
    }

    [GeneratedRegex(@"^(?<base>\d+(\.\d+){1,3})(?:-(?<label>alpha|beta|rc)(?:[-\.]?(?<number>\d+))?)?$",
        RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex VersionRegex();
}
