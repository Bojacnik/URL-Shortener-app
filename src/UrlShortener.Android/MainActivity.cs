using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using UrlShortener.UI;

namespace UrlShortener.Android;

/// <summary>
/// The app's single screen. Also shows up in Android's Share menu: sharing a link from
/// Chrome, YouTube etc. to "URL Shortener" opens the app and shortens it right away.
/// </summary>
[Activity(
    Label = "@string/app_name",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher_round",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
                           | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
[MetaData("android.app.shortcuts", Resource = "@xml/shortcuts")]
[IntentFilter(
    [Intent.ActionSend],
    Categories = [Intent.CategoryDefault],
    DataMimeType = "text/plain",
    Label = "@string/shorten_link")]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Register before base.OnCreate: that is where the view is created and its bindings
        // read CanShare, so the Share buttons only appear if this is already set.
        App.ShareHandler = ShareText;
        App.SystemBarsHandler = SetSystemBarIcons;
        base.OnCreate(savedInstanceState);

        // After a rotation the same intent comes back; only handle a share once.
        if (savedInstanceState is null)
            HandleShareIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleShareIntent(intent);
    }

    private static void HandleShareIntent(Intent? intent)
    {
        if (intent?.Action == Intent.ActionSend
            && intent.GetStringExtra(Intent.ExtraText) is { Length: > 0 } text)
        {
            App.ReceiveSharedText(text);
        }
    }

    /// <summary>Dark theme → light icons; light theme → dark icons, on both the status and navigation bar.</summary>
    private void SetSystemBarIcons(bool darkTheme)
    {
        if (Window is not { } window
            || AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView) is not { } controller)
            return;
        controller.AppearanceLightStatusBars = !darkTheme;
        controller.AppearanceLightNavigationBars = !darkTheme;
    }

    private void ShareText(string text)
    {
        var send = new Intent(Intent.ActionSend);
        send.SetType("text/plain");
        send.PutExtra(Intent.ExtraText, text);
        StartActivity(Intent.CreateChooser(send, UrlShortener.UI.Localization.Loc.Instance["Share.Chooser"]));
    }
}
