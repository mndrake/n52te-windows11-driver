using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace N52te;

/// <summary>A build published on GitHub as a pre-release tagged editor-vX.Y.Z.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string ZipUrl, string SumsUrl, string PageUrl);

/// <summary>Finds new builds of the Editor and Companion on the repository's GitHub releases.</summary>
public static class Updates
{
    public const string Owner = "mndrake";
    public const string Repository = "n52te-windows11-driver";
    public const string TagPrefix = "editor-v";
    public const string ZipAsset = "n52te-standalone.zip";
    public const string SumsAsset = "SHA256SUMS.txt";

    public static string ReleasesApi => $"https://api.github.com/repos/{Owner}/{Repository}/releases?per_page=30";

    public static Version CurrentVersion =>
        Version.TryParse((typeof(Updates).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "")
            .Split('+')[0], out var v) ? v : new Version(0, 0, 0);

    /// <summary>The newest editor release in a GitHub "list releases" response, if it has both assets.</summary>
    public static ReleaseInfo? PickNewest(string releasesJson)
    {
        using var doc = JsonDocument.Parse(releasesJson);
        ReleaseInfo? best = null;
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var tag = r.GetProperty("tag_name").GetString() ?? "";
            if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal) || !Version.TryParse(tag[TagPrefix.Length..], out var version)) continue;
            string? zip = null, sums = null;
            foreach (var a in r.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                var url = a.GetProperty("browser_download_url").GetString();
                if (name == ZipAsset) zip = url;
                else if (name == SumsAsset) sums = url;
            }
            if (zip is null || sums is null) continue;
            if (best is null || version > best.Version)
                best = new ReleaseInfo(version, tag, zip, sums, r.GetProperty("html_url").GetString() ?? "");
        }
        return best;
    }

    /// <summary>Parses "HEX  filename" lines (sha256sum format) into file name → lowercase hash.</summary>
    public static Dictionary<string, string> ParseSums(string text)
    {
        var sums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n'))
        {
            var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0].Length == 64 && parts[0].All(Uri.IsHexDigit))
                sums[parts[1].Trim().TrimStart('*')] = parts[0].ToLowerInvariant();
        }
        return sums;
    }

    public static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("n52te-editor", CurrentVersion.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    public static async Task<ReleaseInfo?> CheckAsync(HttpClient http, CancellationToken cancel = default)
    {
        var json = await http.GetStringAsync(ReleasesApi, cancel);
        var newest = PickNewest(json);
        return newest is not null && newest.Version > CurrentVersion ? newest : null;
    }
}
