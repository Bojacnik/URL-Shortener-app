using UrlShortener.Core.Providers;

namespace UrlShortener.Core;

/// <summary>
/// Validates input, calls the preferred provider and — if that service is down or throttling —
/// falls back to the others in order. Input errors (bad URL, taken alias) never trigger a fallback.
/// </summary>
public sealed class ShortenerService
{
    public ShortenerService(IEnumerable<IShortenerProvider> providers)
    {
        Providers = providers.ToList();
        if (Providers.Count == 0)
            throw new ArgumentException("At least one provider is required.", nameof(providers));
    }

    public IReadOnlyList<IShortenerProvider> Providers { get; }

    public static ShortenerService CreateDefault(HttpClient http) => new(
    [
        new IsGdProvider(http, "is.gd"),
        new TinyUrlProvider(http),
        new DaGdProvider(http),
        new IsGdProvider(http, "v.gd"),
    ]);

    public static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("UrlShortener/2.2 (+https://github.com/Bojacnik/URL-Shortener-app)");
        return http;
    }

    public IShortenerProvider? Find(string? id) =>
        Providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public async Task<ShortenOutcome> ShortenAsync(
        string input,
        string? preferredProviderId = null,
        string? alias = null,
        bool allowFallback = true,
        CancellationToken cancellationToken = default)
    {
        var preferred = Find(preferredProviderId) ?? Providers[0];
        alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();

        if (!UrlValidator.TryNormalize(input, out var longUrl, out string? urlError))
        {
            var fail = ShortenResult.Fail(preferred.Id, FailureKind.InvalidUrl, urlError);
            return new ShortenOutcome(fail, [fail], null);
        }

        if (alias is not null && preferred.ValidateAlias(alias) is { } aliasError)
        {
            var fail = ShortenResult.Fail(preferred.Id, FailureKind.AliasRejected, aliasError);
            return new ShortenOutcome(fail, [fail], longUrl);
        }

        var order = new List<IShortenerProvider> { preferred };
        if (allowFallback)
        {
            // With an alias, only fall back to services that accept that same alias.
            order.AddRange(Providers.Where(p => p != preferred && (alias is null || p.ValidateAlias(alias) is null)));
        }

        var attempts = new List<ShortenResult>();
        foreach (var provider in order)
        {
            var result = await provider.ShortenAsync(longUrl, alias, cancellationToken).ConfigureAwait(false);
            attempts.Add(result);
            if (result.IsSuccess || !result.ShouldTryNextProvider || cancellationToken.IsCancellationRequested)
                break;
        }

        return new ShortenOutcome(attempts[^1], attempts, longUrl);
    }
}
