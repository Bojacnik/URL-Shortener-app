namespace UrlShortener.Core.Providers;

public interface IShortenerProvider
{
    /// <summary>Stable id used in settings and history, e.g. "is.gd".</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>Short note shown in the UI.</summary>
    string Description { get; }

    bool SupportsCustomAlias { get; }

    /// <summary>Checks an alias against this service's rules before any request is made. Returns null when valid.</summary>
    string? ValidateAlias(string alias);

    Task<ShortenResult> ShortenAsync(Uri longUrl, string? alias, CancellationToken cancellationToken = default);
}
