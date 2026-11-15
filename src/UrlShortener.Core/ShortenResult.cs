namespace UrlShortener.Core;

/// <summary>Why a shorten attempt failed. Decides whether trying another provider makes sense.</summary>
public enum FailureKind
{
    None,
    /// <summary>The long URL was rejected. Another provider would most likely reject it too.</summary>
    InvalidUrl,
    /// <summary>The custom alias is malformed or already taken.</summary>
    AliasRejected,
    /// <summary>The service is throttling us.</summary>
    RateLimited,
    /// <summary>The service answered with an error or something we could not understand.</summary>
    ServiceError,
    /// <summary>The service could not be reached (DNS, timeout, TLS, ...).</summary>
    Network,
    Cancelled,
}

public sealed record ShortenResult(string ProviderId, string? ShortUrl, FailureKind Failure, string? Error)
{
    public bool IsSuccess => ShortUrl is not null;

    /// <summary>Failures caused by the service itself, not by the user's input, are worth retrying elsewhere.</summary>
    public bool ShouldTryNextProvider =>
        Failure is FailureKind.RateLimited or FailureKind.ServiceError or FailureKind.Network;

    public static ShortenResult Ok(string providerId, string shortUrl) =>
        new(providerId, shortUrl, FailureKind.None, null);

    public static ShortenResult Fail(string providerId, FailureKind kind, string error) =>
        new(providerId, null, kind, error);
}

/// <summary>Outcome of a whole shorten request, including every provider that was tried.</summary>
public sealed record ShortenOutcome(ShortenResult Final, IReadOnlyList<ShortenResult> Attempts, Uri? LongUrl)
{
    public bool IsSuccess => Final.IsSuccess;
    public bool UsedFallback => Attempts.Count > 1 && Final.IsSuccess;
}
