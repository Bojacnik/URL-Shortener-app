using UrlShortener.Core;

namespace UrlShortener.App.ViewModels;

public sealed class HistoryItemViewModel(HistoryEntry entry)
{
    public HistoryEntry Entry { get; } = entry;
    public string ShortUrl => Entry.ShortUrl;
    public string LongUrl => Entry.LongUrl;
    public string ProviderId => Entry.ProviderId;
    public string When => FormatAge(Entry.CreatedAt, DateTimeOffset.Now);

    public bool Matches(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
        || ShortUrl.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)
        || LongUrl.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

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
