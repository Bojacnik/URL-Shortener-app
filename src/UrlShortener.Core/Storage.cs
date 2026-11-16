using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrlShortener.Core;

public sealed record HistoryEntry(string LongUrl, string ShortUrl, string ProviderId, DateTimeOffset CreatedAt);

public sealed record AppSettings
{
    public string PreferredProviderId { get; init; } = "is.gd";
    public bool AutoFallback { get; init; } = true;
    public bool CopyAutomatically { get; init; } = true;
    public string Theme { get; init; } = "System";
    /// <summary>"system" or a language code such as "cs".</summary>
    public string Language { get; init; } = "system";
}

[JsonSerializable(typeof(List<HistoryEntry>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class StorageJsonContext : JsonSerializerContext;

/// <summary>
/// Keeps history and settings as JSON files in the per-user config folder
/// (~/.config/UrlShortener on Linux, %APPDATA%\UrlShortener on Windows, ~/Library/Application Support on macOS).
/// A missing or corrupt file just means "start fresh".
/// </summary>
public sealed class AppStorage(string? directory = null)
{
    public const int MaxHistory = 200;

    public string Directory { get; } = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "UrlShortener");

    private string HistoryPath => Path.Combine(Directory, "history.json");
    private string SettingsPath => Path.Combine(Directory, "settings.json");

    public async Task<List<HistoryEntry>> LoadHistoryAsync() =>
        await ReadAsync(HistoryPath, StorageJsonContext.Default.ListHistoryEntry).ConfigureAwait(false) ?? [];

    public Task SaveHistoryAsync(IEnumerable<HistoryEntry> entries) =>
        WriteAsync(HistoryPath, entries.Take(MaxHistory).ToList(), StorageJsonContext.Default.ListHistoryEntry);

    public async Task<AppSettings> LoadSettingsAsync() =>
        await ReadAsync(SettingsPath, StorageJsonContext.Default.AppSettings).ConfigureAwait(false) ?? new AppSettings();

    public Task SaveSettingsAsync(AppSettings settings) =>
        WriteAsync(SettingsPath, settings, StorageJsonContext.Default.AppSettings);

    private static async Task<T?> ReadAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
    {
        try
        {
            if (!File.Exists(path)) return default;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, type).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    // Saves are fired without awaiting from the UI; run them one at a time, in call order,
    // so a quick series of changes can't race and leave an older snapshot on disk.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private async Task WriteAsync<T>(string path, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var temp = path + ".tmp";
            await using (var stream = File.Create(temp))
                await JsonSerializer.SerializeAsync(stream, value, type).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting is best-effort; the app keeps working in memory.
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
