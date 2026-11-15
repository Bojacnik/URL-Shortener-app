using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace UrlShortener.Core.Providers;

/// <summary>Shared plumbing: HTTP GET, error mapping, alias rules.</summary>
public abstract partial class ShortenerProviderBase(HttpClient http) : IShortenerProvider
{
    protected HttpClient Http { get; } = http;

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual bool SupportsCustomAlias => true;

    protected virtual int AliasMinLength => 1;
    protected virtual int AliasMaxLength => 30;
    protected virtual bool AliasAllowsDash => true;

    public virtual string? ValidateAlias(string alias)
    {
        if (!SupportsCustomAlias)
            return $"{DisplayName} doesn't support custom aliases.";
        if (alias.Length < AliasMinLength || alias.Length > AliasMaxLength)
            return AliasMinLength == AliasMaxLength
                ? $"{DisplayName} aliases must be {AliasMinLength} characters."
                : $"{DisplayName} aliases must be {AliasMinLength}–{AliasMaxLength} characters.";
        var ok = AliasAllowsDash ? AliasWithDash().IsMatch(alias) : AliasNoDash().IsMatch(alias);
        if (!ok)
            return AliasAllowsDash
                ? "Use only letters, numbers, '-' and '_' in the alias."
                : $"{DisplayName} aliases may contain only letters, numbers and '_'.";
        return null;
    }

    public async Task<ShortenResult> ShortenAsync(Uri longUrl, string? alias, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(alias) && ValidateAlias(alias) is { } aliasError)
            return ShortenResult.Fail(Id, FailureKind.AliasRejected, aliasError);

        try
        {
            using var response = await Http.GetAsync(BuildRequestUri(longUrl, string.IsNullOrEmpty(alias) ? null : alias), cancellationToken)
                .ConfigureAwait(false);
            // Decode as UTF-8 ourselves: is.gd sends "charset=utf8", which ReadAsStringAsync rejects.
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var body = System.Text.Encoding.UTF8.GetString(bytes).Trim();

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return ShortenResult.Fail(Id, FailureKind.RateLimited, $"{DisplayName} is rate limiting requests. Try again in a minute.");
            if ((int)response.StatusCode >= 500)
                return ShortenResult.Fail(Id, FailureKind.ServiceError, $"{DisplayName} is having problems (HTTP {(int)response.StatusCode}).");

            return ParseResponse(response.StatusCode, body, alias);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ShortenResult.Fail(Id, FailureKind.Cancelled, "Cancelled.");
        }
        catch (OperationCanceledException)
        {
            return ShortenResult.Fail(Id, FailureKind.Network, $"{DisplayName} didn't answer in time.");
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException || ex.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError)
        {
            return ShortenResult.Fail(Id, FailureKind.Network, $"Couldn't reach {DisplayName}. Check your internet connection.");
        }
        catch (HttpRequestException ex)
        {
            return ShortenResult.Fail(Id, FailureKind.Network, $"Request to {DisplayName} failed: {ex.Message}");
        }
    }

    protected abstract Uri BuildRequestUri(Uri longUrl, string? alias);

    protected abstract ShortenResult ParseResponse(HttpStatusCode status, string body, string? alias);

    /// <summary>Accepts the service's answer only if it really is a link on the service's own domain.</summary>
    protected ShortenResult AcceptIfShortLink(string body, string expectedHost)
    {
        if (Uri.TryCreate(body, UriKind.Absolute, out var shortUrl)
            && shortUrl.Scheme is "https" or "http"
            && shortUrl.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            return ShortenResult.Ok(Id, shortUrl.ToString());
        }
        return ShortenResult.Fail(Id, FailureKind.ServiceError, $"{DisplayName} returned an unexpected answer.");
    }

    protected static string Escape(string value) => Uri.EscapeDataString(value);

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex AliasWithDash();

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex AliasNoDash();
}
