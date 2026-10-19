using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Toolkit.Uwp.Notifications;

using Serilog;

using Windows.Web.Http;

namespace CalculusLink;

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
    // the newest plugin one (a plain "v1.2" tag with a .tsep asset).
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
                    .AddText($"Version {release.TagName} is ready to install")
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
        client.DefaultRequestHeaders.Add("User-Agent", "CalculusLink");

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

    // "v1.2" → 1.2; anything else (e.g. "desktop-v0.62.0") → null.
    static Version? ParsePluginVersion(string tag) =>
        tag.StartsWith("v", StringComparison.Ordinal)
        && Version.TryParse(tag.Substring(1), out var version)
            ? version
            : null;

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
