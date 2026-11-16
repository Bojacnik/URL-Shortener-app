using System.Net.Http.Headers;
using System.Text.Json;

namespace UrlShortener.Core;

public sealed record UpdateInfo(Version Version, string Tag, Uri ReleasePage);

/// <summary>
/// Asks GitHub whether a newer release exists. That one request is the only thing sent; nothing about the user is.
/// </summary>
public sealed class UpdateChecker(HttpClient http, string repository = UpdateChecker.DefaultRepository)
{
    public const string DefaultRepository = "Bojacnik/URL-Shortener-app";
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    /// <summary>The newest published release if it's newer than <paramref name="current"/>, otherwise null. Never throws.</summary>
    public async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("UrlShortener-update-check");
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null; // 404 = no releases yet

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!TryParseVersion(tag, out var latest) || latest <= Normalize(current)) return null;

            var page = root.TryGetProperty("html_url", out var url) && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var u)
                ? u : new Uri($"https://github.com/{repository}/releases/latest");
            return new UpdateInfo(latest, tag, page);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException or KeyNotFoundException or InvalidOperationException)
        {
            return null; // offline, rate-limited, unexpected answer: just try again another day
        }
    }

    public static bool IsDue(DateTimeOffset? lastCheck, DateTimeOffset now) =>
        lastCheck is null || now - lastCheck.Value >= CheckInterval;

    internal static bool TryParseVersion(string tag, out Version version)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOf('-');
        if (dash >= 0) text = text[..dash];
        if (Version.TryParse(text, out var v))
        {
            version = Normalize(v);
            return true;
        }
        version = new Version(0, 0, 0);
        return false;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
