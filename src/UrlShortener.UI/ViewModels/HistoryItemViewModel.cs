using CommunityToolkit.Mvvm.ComponentModel;
using UrlShortener.Core;
using UrlShortener.UI.Localization;

namespace UrlShortener.UI.ViewModels;

public sealed class HistoryItemViewModel(HistoryEntry entry) : ObservableObject
{
    public HistoryEntry Entry { get; private set; } = entry;

    public bool Pinned => Entry.Pinned;

    internal void SetPinned(bool pinned)
    {
        Entry = Entry with { Pinned = pinned };
        OnPropertyChanged(nameof(Pinned));
    }
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

    /// <summary>Re-reads the translated "… ago" text after a language change.</summary>
    public void Refresh() => OnPropertyChanged(nameof(When));

    public bool Matches(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
        || ShortUrl.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)
        || LongUrl.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string StripScheme(string url)
    {
        var i = url.IndexOf("://", StringComparison.Ordinal);
        return i >= 0 ? url[(i + 3)..] : url;
    }

    internal static string FormatAge(DateTimeOffset then, DateTimeOffset now)
    {
        var loc = Loc.Instance;
        var age = now - then;
        if (age < TimeSpan.FromMinutes(1)) return loc["Age.Now"];
        if (age < TimeSpan.FromHours(1)) return loc.Format("Age.Minutes", (int)age.TotalMinutes);
        if (age < TimeSpan.FromDays(1)) return loc.Format("Age.Hours", (int)age.TotalHours);
        if (age < TimeSpan.FromDays(7)) return loc.Format("Age.Days", (int)age.TotalDays);
        return then.LocalDateTime.ToString("d MMM yyyy", loc.Culture);
    }
}
