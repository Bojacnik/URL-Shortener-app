using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UrlShortener.Core;

/// <summary>How following a link ended.</summary>
public enum ExpandStop
{
    /// <summary>Reached an address that doesn't redirect any further.</summary>
    Done,
    /// <summary>The next hop is a local or private address; we report it but don't contact it.</summary>
    PrivateAddress,
    TooManyRedirects,
    Loop,
    /// <summary>The first address couldn't be reached at all.</summary>
    Failed,
}

/// <param name="Chain">Every address visited, starting with the one given. The last one is the destination.</param>
public sealed record ExpandResult(IReadOnlyList<Uri> Chain, ExpandStop Stop, string? Error = null)
{
    public Uri Final => Chain[^1];
    public int Redirects => Chain.Count - 1;
    public bool IsSuccess => Stop != ExpandStop.Failed;
}

/// <summary>
/// Shows where a (short) link really goes without opening it in a browser: follows HTTP redirects
/// one hop at a time using HEAD requests, and never downloads page content.
/// </summary>
public sealed partial class LinkExpander(HttpClient http)
{
    public const int MaxRedirects = 10;
    private const int MetaRefreshScanBytes = 64 * 1024;

    /// <summary>An HttpClient that does NOT follow redirects by itself, which this class needs.</summary>
    public static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("UrlShortener/2.2 (+https://github.com/Bojacnik/URL-Shortener-app)");
        return client;
    }

    public async Task<ExpandResult> ExpandAsync(Uri start, CancellationToken cancellationToken = default)
    {
        var chain = new List<Uri> { start };
        var current = start;

        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            Uri? next;
            try
            {
                next = await NextHopAsync(current, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // If the very first request fails we know nothing. Later on, the chain so far is still useful:
                // it tells you where the short link points even if that site is down.
                return chain.Count == 1
                    ? new ExpandResult(chain, ExpandStop.Failed, ex is OperationCanceledException ? "timeout" : ex.Message)
                    : new ExpandResult(chain, ExpandStop.Done);
            }

            if (next is null)
                return new ExpandResult(chain, ExpandStop.Done);

            if (chain.Contains(next))
                return new ExpandResult(chain, ExpandStop.Loop);

            chain.Add(next);
            if (UrlValidator.IsLocalOrPrivate(next))
                return new ExpandResult(chain, ExpandStop.PrivateAddress);
            if (hop == MaxRedirects)
                break;

            current = next;
        }

        return new ExpandResult(chain, ExpandStop.TooManyRedirects);
    }

    /// <summary>Where <paramref name="url"/> sends you next, or null if it's the end of the line.</summary>
    private async Task<Uri?> NextHopAsync(Uri url, CancellationToken ct)
    {
        // is.gd and v.gd have an API for this; v.gd otherwise shows a preview page instead of redirecting.
        if (url.Host is "is.gd" or "v.gd" && url.AbsolutePath.Trim('/') is { Length: > 0 } code && !code.Contains('/'))
        {
            var viaApi = await IsGdLookupAsync(url.Host, code, ct).ConfigureAwait(false);
            if (viaApi is not null) return viaApi;
        }

        using var head = new HttpRequestMessage(HttpMethod.Head, url);
        using var response = await http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        // Some servers don't do HEAD properly; ask again with GET (headers only, body is not downloaded).
        if ((int)response.StatusCode is 400 or 403 or 404 or 405 or 501)
        {
            using var get = new HttpRequestMessage(HttpMethod.Get, url);
            using var getResponse = await http.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return await NextFromResponseAsync(url, getResponse, isGet: true, ct).ConfigureAwait(false);
        }

        return await NextFromResponseAsync(url, response, isGet: false, ct).ConfigureAwait(false);
    }

    private static async Task<Uri?> NextFromResponseAsync(Uri url, HttpResponseMessage response, bool isGet, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        if (status is >= 300 and < 400 && response.Headers.Location is { } location)
            return AsHttpUrl(location.IsAbsoluteUri ? location : new Uri(url, location));

        // Pages that redirect with <meta http-equiv="refresh">. Only for GET answers that are HTML.
        if (status == 200
            && isGet
            && response.Content.Headers.ContentType?.MediaType == "text/html")
        {
            var html = await ReadPrefixAsync(response.Content, MetaRefreshScanBytes, ct).ConfigureAwait(false);
            var match = MetaRefresh().Match(html);
            if (match.Success && Uri.TryCreate(url, WebUtility.HtmlDecode(match.Groups["url"].Value.Trim()), out var target))
                return AsHttpUrl(target);
        }
        return null;
    }

    private async Task<Uri?> IsGdLookupAsync(string host, string code, CancellationToken ct)
    {
        try
        {
            var api = new Uri($"https://{host}/forward.php?format=json&shorturl={Uri.EscapeDataString(code)}");
            using var response = await http.GetAsync(api, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(bytes);
            return doc.RootElement.TryGetProperty("url", out var u) && Uri.TryCreate(u.GetString(), UriKind.Absolute, out var target)
                ? AsHttpUrl(target)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException)
        {
            return null; // fall back to plain HTTP
        }
    }

    private static Uri? AsHttpUrl(Uri uri) => uri.Scheme is "http" or "https" ? uri : null;

    private static async Task<string> ReadPrefixAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[maxBytes];
        var total = 0;
        int read;
        while (total < maxBytes && (read = await stream.ReadAsync(buffer.AsMemory(total, maxBytes - total), ct).ConfigureAwait(false)) > 0)
            total += read;
        return System.Text.Encoding.UTF8.GetString(buffer, 0, total);
    }

    [GeneratedRegex("""<meta[^>]+http-equiv\s*=\s*["']?refresh["']?[^>]*content\s*=\s*["']?\s*\d*\s*;?\s*url\s*=\s*["']?(?<url>[^"'>]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaRefresh();
}
