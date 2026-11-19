using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Toolkit.Uwp.Notifications;

using Serilog;

using Windows.Web.Http;

namespace CalculusTeklaLink;

public class GithubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("assets")]
    public List<GithubAsset> Assets { get; set; } = [];
}

public class GithubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = string.Empty;
}

public static class Updater
{
    // The public repo also carries desktop app releases (tags "desktop-v…"),
    // so "the latest release" may not be ours: list recent releases and take
    // the newest plugin one with a .tsep asset. Plugin tags are
    // "tekla-link-v1.4" from 1.4 on; 1.0–1.3 used a plain "v1.3".
    const string Url = "https://api.github.com/repos/mikl0906/calculus-public/releases?per_page=50";

    public static async Task CheckForUpdates(bool showSuccessToast = false)
    {
        try
        {
            var release = await GetLatestReleaseAsync();

            if (release == null)
            {
                Log.Error("Failed to retrieve latest release information.");
                return;
            }

            if (IsNewerVersion(release.TagName))
            {
                Log.Information("Update available.");

                new ToastContentBuilder()
                    .AddText("New version available")
                    .AddText($"Version {FormatVersion(ParsePluginVersion(release.TagName))} is ready to install")
                    .AddButton(new ToastButton()
                        .SetContent("Download")
                        .AddArgument("action", "update"))
                    .Show();
            }
            else
            {
                Log.Information("No updates available.");

                if (showSuccessToast)
                {
                    new ToastContentBuilder()
                        .AddText("No updates available")
                        .AddText("You are currently using the latest version")
                        .Show();
                }
            }
        }
        catch
        {
            Log.Error("Failed to check for updates.");
        }
    }

    public static async Task DownloadLatestTsep()
    {
        try
        {
            var release = await GetLatestReleaseAsync();

            if (release == null)
            {
                Log.Error("Failed to retrieve latest release information.");
                return;
            }

            var downloadUrl = GetTsepDownloadUrl(release);

            Process.Start(new ProcessStartInfo
            {
                FileName = downloadUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            Log.Error("Failed to check for updates.");
        }
    }

    static async Task<GithubRelease?> GetLatestReleaseAsync()
    {
        using var client = new HttpClient();

        // REQUIRED by GitHub API
        client.DefaultRequestHeaders.Add("User-Agent", "CalculusTeklaLink");

        var response = await client.GetAsync(new Uri(Url));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var releases = JsonSerializer.Deserialize<List<GithubRelease>>(json) ?? [];

        return releases
            .Where(r => !r.Draft && !r.Prerelease && ParsePluginVersion(r.TagName) != null)
            .Where(r => r.Assets.Any(a => a.Name.EndsWith(".tsep", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => ParsePluginVersion(r.TagName))
            .FirstOrDefault();
    }

    // "tekla-link-v1.4" or "v1.3" → 1.4 / 1.3; anything else
    // (e.g. "desktop-v0.62.0", "desktop-updater") → null.
    internal static Version? ParsePluginVersion(string tag)
    {
        foreach (var prefix in new[] { "tekla-link-v", "v" })
        {
            if (tag.StartsWith(prefix, StringComparison.Ordinal)
                && Version.TryParse(tag.Substring(prefix.Length), out var version))
                return version;
        }
        return null;
    }

    static string FormatVersion(Version? version) =>
        version == null ? "" : $"{version.Major}.{version.Minor}";

    static bool IsNewerVersion(string tag)
    {
        var latest = ParsePluginVersion(tag);
        if (latest == null)
            return false;

        var current =
            Assembly.GetExecutingAssembly()
            .GetName()
            .Version;

        return latest > current;
    }

    static string GetTsepDownloadUrl(GithubRelease release)
    {
        var asset = release.Assets
            .First(a => a.Name.EndsWith(".tsep",
                StringComparison.OrdinalIgnoreCase));

        return asset.DownloadUrl;
    }
}
