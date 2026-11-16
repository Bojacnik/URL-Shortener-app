using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.QuickSettings;
using System.Runtime.Versioning;

namespace UrlShortener.Android;

/// <summary>A Quick Settings tile ("Shorten clipboard") — pull down the notification shade, tap, done.</summary>
[Service(
    Name = "com.bojacnik.urlshortener.ShortenTileService",
    Label = "@string/shorten_clipboard",
    Icon = "@drawable/ic_tile",
    Permission = "android.permission.BIND_QUICK_SETTINGS_TILE",
    Exported = true)]
[IntentFilter([TileService.ActionQsTile])]
[SupportedOSPlatform("android24.0")] // Quick Settings tiles arrived in Android 7; older phones simply don't show it.
public class ShortenTileService : TileService
{
    public override void OnStartListening()
    {
        base.OnStartListening();
        if (QsTile is { } tile)
        {
            tile.State = TileState.Inactive;
            tile.UpdateTile();
        }
    }

    public override void OnClick()
    {
        base.OnClick();
        var intent = new Intent(this, typeof(ClipboardActivity)).AddFlags(ActivityFlags.NewTask);

        // The shade must close so the (invisible) activity can take focus and read the clipboard.
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            var pending = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            StartActivityAndCollapse(pending!);
        }
        else
        {
#pragma warning disable CA1422, CS0618 // the Intent overload is the only one before Android 14 (deprecated since)
            StartActivityAndCollapse(intent);
#pragma warning restore CA1422, CS0618
        }
    }
}
