namespace UrlShortener.App.Services;

/// <summary>Things the view model needs from the UI platform. Faked in tests.</summary>
public interface IPlatformServices
{
    Task CopyTextAsync(string text);
    Task<string?> GetClipboardTextAsync();
    Task<bool> OpenUrlAsync(string url);
    void SetTheme(string theme);
}
