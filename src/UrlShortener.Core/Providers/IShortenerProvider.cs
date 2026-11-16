namespace UrlShortener.Core.Providers;

/// <summary>What's wrong with a custom alias. The UI turns these into translated messages.</summary>
public enum AliasProblem
{
    NotSupported,
    Length,
    Characters,
}

/// <summary>A service's rules for custom aliases.</summary>
public sealed record AliasRule(int MinLength, int MaxLength, bool AllowsDash);

public interface IShortenerProvider
{
    /// <summary>Stable id used in settings and history, e.g. "is.gd".</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>Short English note (the UI shows a translated one).</summary>
    string Description { get; }

    bool SupportsCustomAlias { get; }

    /// <summary>Alias rules, or null when custom aliases aren't supported.</summary>
    AliasRule? AliasRule { get; }

    /// <summary>Checks an alias against this service's rules. Returns null when it's fine.</summary>
    AliasProblem? CheckAlias(string alias);

    /// <summary>Same check with an English message; null when valid.</summary>
    string? ValidateAlias(string alias);

    Task<ShortenResult> ShortenAsync(Uri longUrl, string? alias, CancellationToken cancellationToken = default);
}
