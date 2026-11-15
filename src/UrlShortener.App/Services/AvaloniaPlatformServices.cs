using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Styling;

namespace UrlShortener.App.Services;

public sealed class AvaloniaPlatformServices(TopLevel topLevel) : IPlatformServices
{
    public async Task CopyTextAsync(string text)
    {
        if (topLevel.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public async Task<string?> GetClipboardTextAsync() =>
        topLevel.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;

    public async Task<bool> OpenUrlAsync(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && await topLevel.Launcher.LaunchUriAsync(uri);

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
}
