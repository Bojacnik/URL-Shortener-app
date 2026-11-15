using System.Net;
using System.Text.Json;

namespace UrlShortener.Core.Providers;

/// <summary>
/// is.gd and its sister service v.gd (same operator and API, running since 2009). Free, no API key.
/// API docs: https://is.gd/apishorteningreference.php
/// </summary>
public sealed class IsGdProvider : ShortenerProviderBase
{
    private readonly string _host;

    public IsGdProvider(HttpClient http, string host = "is.gd") : base(http)
    {
        if (host is not ("is.gd" or "v.gd"))
            throw new ArgumentException("Host must be is.gd or v.gd.", nameof(host));
        _host = host;
    }

    public override string Id => _host;
    public override string DisplayName => _host;
    public override string Description => _host == "is.gd"
        ? "Free, no account, running since 2009. Supports custom aliases."
        : "is.gd's sister service: shows a preview page before redirecting.";

    // is.gd: 5–30 characters, letters, numbers and underscores.
    protected override int AliasMinLength => 5;
    protected override int AliasMaxLength => 30;
    protected override bool AliasAllowsDash => false;

    protected override Uri BuildRequestUri(Uri longUrl, string? alias)
    {
        var uri = $"https://{_host}/create.php?format=json&url={Escape(longUrl.AbsoluteUri)}";
        if (alias is not null)
            uri += $"&shorturl={Escape(alias)}";
        return new Uri(uri);
    }

    protected override ShortenResult ParseResponse(HttpStatusCode status, string body, string? alias)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("shorturl", out var shortUrl) && shortUrl.GetString() is { } value)
                return AcceptIfShortLink(value, _host);

            if (root.TryGetProperty("errorcode", out var codeEl) && codeEl.TryGetInt32(out var code))
            {
                var message = root.TryGetProperty("errormessage", out var m) ? m.GetString() ?? "" : "";
                // Codes per API docs: 1 = bad long URL, 2 = bad/taken short URL, 3 = rate limited, 4 = other.
                return code switch
                {
                    1 => ShortenResult.Fail(Id, FailureKind.InvalidUrl, $"{DisplayName}: {message}"),
                    2 => ShortenResult.Fail(Id, FailureKind.AliasRejected, $"{DisplayName}: {message}"),
                    3 => ShortenResult.Fail(Id, FailureKind.RateLimited, $"{DisplayName} is rate limiting requests. Try again in a minute."),
                    _ => ShortenResult.Fail(Id, FailureKind.ServiceError, $"{DisplayName}: {message}"),
                };
            }
        }
        catch (JsonException)
        {
            // fall through
        }
        return ShortenResult.Fail(Id, FailureKind.ServiceError, $"{DisplayName} returned an unexpected answer (HTTP {(int)status}).");
    }
}
