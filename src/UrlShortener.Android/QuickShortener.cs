using Android.Content;
using Android.Widget;
using UrlShortener.Core;
using UrlShortener.UI.Localization;
using UrlShortener.UI.ViewModels;

namespace UrlShortener.Android;

/// <summary>
/// Shortening without opening the app: used by the text-selection action, the Quick Settings tile and the
/// launcher shortcut. Uses the same settings, services and history as the app.
/// </summary>
internal static class QuickShortener
{
    private static readonly Lazy<HttpClient> Http = new(ShortenerService.CreateHttpClient);

    /// <summary>Shortens the first link in <paramref name="text"/>. Returns the short link, or null with a message.</summary>
    public static async Task<(string? ShortUrl, string Message)> ShortenAsync(string? text)
    {
        var storage = new AppStorage();
        var settings = await storage.LoadSettingsAsync();
        Loc.Instance.SetLanguage(settings.Language);

        var candidate = UrlValidator.ExtractUrl(text);
        if (!UrlValidator.TryNormalize(candidate, out var url, out UrlProblem _))
            return (null, Loc.Instance["Quick.NoLink"]);

        var shortener = ShortenerService.CreateDefault(Http.Value);
        var outcome = await shortener.ShortenAsync(url.AbsoluteUri, settings.PreferredProviderId, null, settings.AutoFallback);
        if (!outcome.IsSuccess)
            return (null, Messages.Failure(outcome.Final, shortener).ToString());

        var shortUrl = outcome.Final.ShortUrl!;
        var history = await storage.LoadHistoryAsync();
        var pinned = history.Any(h => h.Pinned && string.Equals(h.ShortUrl, shortUrl, StringComparison.OrdinalIgnoreCase));
        history.RemoveAll(h => string.Equals(h.ShortUrl, shortUrl, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, new HistoryEntry(url.AbsoluteUri, shortUrl, outcome.Final.ProviderId, DateTimeOffset.Now, pinned));
        await storage.SaveHistoryAsync(history.OrderByDescending(h => h.Pinned).ThenByDescending(h => h.CreatedAt));

        return (shortUrl, $"{Loc.Instance["Quick.Done"]}: {HistoryItemViewModel.StripScheme(shortUrl)}");
    }

    public static void CopyToClipboard(Context context, string text)
    {
        if (context.GetSystemService(Context.ClipboardService) is ClipboardManager clipboard)
            clipboard.PrimaryClip = ClipData.NewPlainText(Loc.Instance["App.Title"], text);
    }

    public static void Toast(Context context, string message) =>
        global::Android.Widget.Toast.MakeText(context, message, ToastLength.Long)?.Show();
}
