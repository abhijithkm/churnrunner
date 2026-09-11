using System.Net.Http;
using System.Text.Json;

namespace ChurnRunner;

public static class UpdateChecker
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/abhijithkm/churnrunner/releases/latest";
    public const string ReleasesPageUrl = "https://github.com/abhijithkm/churnrunner/releases/latest";

    public record UpdateInfo(string LatestVersion, string Url);

    public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken token = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ChurnRunner");

            var json = await http.GetStringAsync(ReleasesApiUrl, token);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var latest = tag.TrimStart('v', 'V');

            var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

            if (Version.TryParse(latest, out var latestVersion) && latestVersion > current)
                return new UpdateInfo(latest, ReleasesPageUrl);
        }
        catch
        {
            // Offline, rate-limited, or no releases published yet - not worth surfacing to the user.
        }

        return null;
    }
}
