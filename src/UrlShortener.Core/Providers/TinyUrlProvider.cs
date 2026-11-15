using System.Net;

namespace UrlShortener.Core.Providers;

/// <summary>
/// TinyURL's keyless endpoint (api-create.php). TinyURL has been running since 2002.
/// Answers with the short link as plain text, or "Error" with HTTP 400.
/// </summary>
public sealed class TinyUrlProvider(HttpClient http) : ShortenerProviderBase(http)
{
    public override string Id => "tinyurl";
    public override string DisplayName => "TinyURL";
    public override string Description => "The oldest shortener (2002). Free, no account. Supports custom aliases.";

    protected override int AliasMinLength => 5;
    protected override int AliasMaxLength => 30;

    protected override Uri BuildRequestUri(Uri longUrl, string? alias)
    {
        var uri = $"https://tinyurl.com/api-create.php?url={Escape(longUrl.AbsoluteUri)}";
        if (alias is not null)
            uri += $"&alias={Escape(alias)}";
        return new Uri(uri);
    }

    protected override ShortenResult ParseResponse(HttpStatusCode status, string body, string? alias)
    {
        if (status == HttpStatusCode.OK)
            return AcceptIfShortLink(body, "tinyurl.com");

        if (status == HttpStatusCode.BadRequest)
        {
            // The legacy API gives no detail. With an alias, a taken alias is by far the likeliest cause.
            return alias is not null
                ? ShortenResult.Fail(Id, FailureKind.AliasRejected, "TinyURL rejected the request — that alias is probably taken.")
                : ShortenResult.Fail(Id, FailureKind.InvalidUrl, "TinyURL rejected this URL.");
        }

        return ShortenResult.Fail(Id, FailureKind.ServiceError, $"TinyURL returned HTTP {(int)status}.");
    }
}
