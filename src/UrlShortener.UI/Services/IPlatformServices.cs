namespace UrlShortener.UI.Services;

/// <summary>Things the view model needs from the UI platform. Faked in tests.</summary>
public interface IPlatformServices
{
    Task CopyTextAsync(string text);
    Task<string?> GetClipboardTextAsync();
    Task<bool> OpenUrlAsync(string url);
    void SetTheme(string theme);

    /// <summary>True where the OS has a share sheet (Android).</summary>
    bool CanShare { get; }
    void Share(string text);

    /// <summary>Asks where to save, then writes the file. False if cancelled or it failed.</summary>
    Task<bool> SaveFileAsync(string suggestedName, FileKind kind, byte[] content);

    /// <summary>Asks for a file and returns its text, or null if cancelled.</summary>
    Task<string?> OpenTextFileAsync(FileKind kind);
}

public enum FileKind { Csv, Png }
