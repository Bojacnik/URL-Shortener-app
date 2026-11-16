using Android.App;
using Android.Content;
using Android.OS;

namespace UrlShortener.Android;

/// <summary>
/// Shortens the link on the clipboard and copies the short link back, with only a toast on screen.
/// Opened by the Quick Settings tile and the launcher shortcut. Android 10+ only lets the focused app read
/// the clipboard, so this waits until its (invisible) window has focus.
/// </summary>
[Activity(
    Name = "com.bojacnik.urlshortener.ClipboardActivity",
    Label = "@string/shorten_clipboard",
    Theme = "@style/Theme.Invisible",
    Exported = true,
    NoHistory = true,
    ExcludeFromRecents = true,
    TaskAffinity = "")]
public class ClipboardActivity : Activity
{
    private bool _started;

    public override async void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (!hasFocus || _started) return;
        _started = true;

        string? text = null;
        if (GetSystemService(ClipboardService) is ClipboardManager clipboard && clipboard.PrimaryClip is { ItemCount: > 0 } clip)
            text = clip.GetItemAt(0)?.CoerceToText(this)?.ToString();

        var (shortUrl, message) = await QuickShortener.ShortenAsync(text);
        if (shortUrl is not null)
            QuickShortener.CopyToClipboard(this, shortUrl);
        QuickShortener.Toast(this, message);
        Finish();
    }
}
