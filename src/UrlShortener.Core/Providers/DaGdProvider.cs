using System.Net;

namespace UrlShortener.Core.Providers;

/// <summary>
/// da.gd — small open-source shortener (github.com/relrod/dagd). Plain-text API, no key.
/// </summary>
public sealed class DaGdProvider(HttpClient http) : ShortenerProviderBase(http)
{
    public override string Id => "da.gd";
    public override string DisplayName => "da.gd";
    public override string Description => "Open-source and minimal. Aliases up to 10 characters.";

    // da.gd silently truncates longer aliases, so refuse them up front.
    protected override int AliasMinLength => 1;
    protected override int AliasMaxLength => 10;

    protected override Uri BuildRequestUri(Uri longUrl, string? alias)
    {
        var uri = $"https://da.gd/s?url={Escape(longUrl.AbsoluteUri)}";
        if (alias is not null)
            uri += $"&shorturl={Escape(alias)}";
        return new Uri(uri);
    }

    protected override ShortenResult ParseResponse(HttpStatusCode status, string body, string? alias)
    {
        if (status == HttpStatusCode.OK)
            return AcceptIfShortLink(body, "da.gd");

        if (status == HttpStatusCode.BadRequest)
        {
            var message = body.Length is > 0 and < 200 ? body : "request rejected";
            var kind = body.Contains("short url", StringComparison.OrdinalIgnoreCase) ? FailureKind.AliasRejected : FailureKind.InvalidUrl;
            return ShortenResult.Fail(Id, kind, $"da.gd: {message}");
        }

        return ShortenResult.Fail(Id, FailureKind.ServiceError, $"da.gd returned HTTP {(int)status}.");
    }
}
