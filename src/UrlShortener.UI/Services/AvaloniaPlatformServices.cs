using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Styling;

namespace UrlShortener.UI.Services;

/// <summary>
/// Clipboard, browser and theme through Avalonia's TopLevel. The TopLevel is looked up on each call
/// because on Android the activity (and with it the view) can be recreated.
/// </summary>
public sealed class AvaloniaPlatformServices(Func<TopLevel?> topLevel) : IPlatformServices
{
    public async Task CopyTextAsync(string text)
    {
        if (topLevel()?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public async Task<string?> GetClipboardTextAsync() =>
        topLevel()?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;

    public async Task<bool> OpenUrlAsync(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && topLevel() is { } top
        && await top.Launcher.LaunchUriAsync(uri);

    public void SetTheme(string theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }

    public bool CanShare => App.ShareHandler is not null;

    public void Share(string text) => App.ShareHandler?.Invoke(text);
}
