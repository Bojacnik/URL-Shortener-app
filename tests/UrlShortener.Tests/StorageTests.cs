using UrlShortener.Core;

namespace UrlShortener.Tests;

public class StorageTests
{
    [Fact]
    public async Task Roundtrips_history_and_settings()
    {
        using var dir = new TempDir();
        var storage = new AppStorage(dir.Path);

        var entry = new HistoryEntry("https://example.com/", "https://is.gd/x", "is.gd", DateTimeOffset.Parse("2026-11-15T10:00:00+01:00"));
        await storage.SaveHistoryAsync([entry]);
        await storage.SaveSettingsAsync(new AppSettings { PreferredProviderId = "tinyurl", AutoFallback = false, Theme = "Dark" });

        Assert.Equal([entry], await storage.LoadHistoryAsync());
        var settings = await storage.LoadSettingsAsync();
        Assert.Equal("tinyurl", settings.PreferredProviderId);
        Assert.False(settings.AutoFallback);
        Assert.Equal("Dark", settings.Theme);
    }

    [Fact]
    public async Task Missing_or_corrupt_files_give_defaults()
    {
        using var dir = new TempDir();
        var storage = new AppStorage(dir.Path);
        Assert.Empty(await storage.LoadHistoryAsync());
        Assert.Equal(new AppSettings(), await storage.LoadSettingsAsync());

        Directory.CreateDirectory(dir.Path);
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "history.json"), "{ not json");
        Assert.Empty(await storage.LoadHistoryAsync());
    }

    [Fact]
    public async Task History_is_capped()
    {
        using var dir = new TempDir();
        var storage = new AppStorage(dir.Path);
        var many = Enumerable.Range(0, AppStorage.MaxHistory + 50)
            .Select(i => new HistoryEntry($"https://example.com/{i}", $"https://is.gd/{i}", "is.gd", DateTimeOffset.Now));
        await storage.SaveHistoryAsync(many);

        Assert.Equal(AppStorage.MaxHistory, (await storage.LoadHistoryAsync()).Count);
    }
}
