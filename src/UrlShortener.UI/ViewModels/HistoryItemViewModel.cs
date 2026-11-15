using UrlShortener.Core;

namespace UrlShortener.UI.ViewModels;

public sealed class HistoryItemViewModel(HistoryEntry entry)
{
    public HistoryEntry Entry { get; } = entry;
    public string ShortUrl => Entry.ShortUrl;
    public string LongUrl => Entry.LongUrl;
    public string ProviderId => Entry.ProviderId;
    public string When => FormatAge(Entry.CreatedAt, DateTimeOffset.Now);

    /// <summary>"is.gd/abc" — the link without its scheme, for display.</summary>
    public string ShortDisplay => StripScheme(ShortUrl);

    /// <summary>"github.com/user/repo" — the long link without scheme or "www.".</summary>
    public string LongDisplay
    {
        get
        {
            var s = StripScheme(LongUrl);
            if (s.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) s = s[4..];
            return s.TrimEnd('/');
        }
    }

    public string Host => Uri.TryCreate(LongUrl, UriKind.Absolute, out var u)
        ? (u.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? u.Host[4..] : u.Host)
        : "";

    /// <summary>Letter shown in the round site badge.</summary>
    public string Initial => Host.Length > 0 ? char.ToUpperInvariant(Host[0]).ToString() : "#";

    /// <summary>Stable hue (0–359) per site, so the same site always gets the same badge colour.</summary>
    public int Hue
    {
        get
        {
            uint hash = 2166136261;
            foreach (var c in Host) hash = (hash ^ c) * 16777619;
            return (int)(hash % 360);
        }
    }

    public bool Matches(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
        || ShortUrl.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)
        || LongUrl.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    internal static string StripScheme(string url)
    {
        var i = url.IndexOf("://", StringComparison.Ordinal);
        return i >= 0 ? url[(i + 3)..] : url;
    }

    internal static string FormatAge(DateTimeOffset then, DateTimeOffset now)
    {
        var age = now - then;
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min ago";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours} h ago";
        if (age < TimeSpan.FromDays(7)) return $"{(int)age.TotalDays} d ago";
        return then.LocalDateTime.ToString("d MMM yyyy");
    }
}
