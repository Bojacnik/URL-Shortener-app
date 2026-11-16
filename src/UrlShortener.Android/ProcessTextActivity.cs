using Android.App;
using Android.Content;
using Android.OS;

namespace UrlShortener.Android;

/// <summary>
/// "Shorten link" in the menu that appears when you select text in any app. In editable fields the selected
/// link is replaced with the short one; elsewhere the short link is copied to the clipboard.
/// No window is shown, only a toast.
/// </summary>
[Activity(
    Name = "com.bojacnik.urlshortener.ProcessTextActivity",
    Label = "@string/shorten_link",
    Theme = "@style/Theme.Invisible",
    Exported = true,
    NoHistory = true,
    ExcludeFromRecents = true)]
[IntentFilter([Intent.ActionProcessText], Categories = [Intent.CategoryDefault], DataMimeType = "text/plain")]
public class ProcessTextActivity : Activity
{
    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var text = Intent?.GetCharSequenceExtra(Intent.ExtraProcessText)?.ToString();
        var readOnly = Intent?.GetBooleanExtra(Intent.ExtraProcessTextReadonly, false) ?? true;

        var (shortUrl, message) = await QuickShortener.ShortenAsync(text);
        if (shortUrl is not null)
        {
            QuickShortener.CopyToClipboard(this, shortUrl);
            if (!readOnly)
            {
                // Hand the short link back: the app the text came from replaces the selection with it.
                var result = new Intent();
                result.PutExtra(Intent.ExtraProcessText, shortUrl);
                SetResult(Result.Ok, result);
            }
        }
        QuickShortener.Toast(this, message);
        Finish();
    }
}
