using System.Net;
using UrlShortener.Core;

namespace UrlShortener.Tests;

public class HistoryCsvTests
{
    private static readonly DateTimeOffset When = DateTimeOffset.Parse("2026-11-16T10:30:00+01:00");

    [Fact]
    public void Roundtrips_including_awkward_characters()
    {
        HistoryEntry[] entries =
        [
            new("https://example.com/a?x=1,2&q=\"quoted\"", "https://is.gd/a", "is.gd", When, Pinned: true),
            new("https://example.com/b", "https://tinyurl.com/b", "tinyurl", When.AddDays(-1)),
        ];
        var csv = HistoryCsv.Export(entries);

        Assert.StartsWith("created_at,provider,short_url,long_url,pinned\r\n", csv);
        Assert.Equal(entries, HistoryCsv.Import(csv));
    }

    [Fact]
    public void Imports_spreadsheet_edited_files()
    {
        // Columns reordered, BOM, blank line, an extra column, a junk row, and no dates or providers.
        const string csv = "﻿long_url;x,short_url,note\n\nhttps://example.com/1,https://da.gd/one,hi\nnot a url,also not,\n";
        var csvComma = csv.Replace("long_url;x", "long_url");
        var imported = HistoryCsv.Import(csvComma);

        var entry = Assert.Single(imported);
        Assert.Equal("https://da.gd/one", entry.ShortUrl);
        Assert.Equal("da.gd", entry.ProviderId);
        Assert.False(entry.Pinned);
    }

    [Fact]
    public void Rejects_files_that_are_not_history()
    {
        Assert.Throws<FormatException>(() => HistoryCsv.Import("name,age\nAda,36"));
        Assert.Throws<FormatException>(() => HistoryCsv.Import(""));
    }

    [Fact]
    public void Merge_dedupes_keeps_pins_and_sorts()
    {
        var existing = new[] { new HistoryEntry("https://e.com/1", "https://is.gd/1", "is.gd", When) };
        var imported = new[]
        {
            new HistoryEntry("https://e.com/1", "https://IS.GD/1", "is.gd", When, Pinned: true),
            new HistoryEntry("https://e.com/2", "https://is.gd/2", "is.gd", When.AddDays(1)),
        };
        var merged = HistoryCsv.Merge(existing, imported);

        Assert.Equal(["https://is.gd/1", "https://is.gd/2"], merged.Select(e => e.ShortUrl)); // pinned first
        Assert.True(merged[0].Pinned);
    }

    [Fact]
    public async Task Old_history_files_without_pinned_still_load()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Path);
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "history.json"),
            """[{ "longUrl": "https://e.com/", "shortUrl": "https://is.gd/x", "providerId": "is.gd", "createdAt": "2026-11-15T10:00:00+00:00" }]""");

        var entry = Assert.Single(await new AppStorage(dir.Path).LoadHistoryAsync());
        Assert.False(entry.Pinned);
    }
}

public class UpdateCheckerTests
{
    private static FakeHttpHandler Release(string tag, bool prerelease = false) =>
        new FakeHttpHandler().On("api.github.com", HttpStatusCode.OK,
            $$"""{ "tag_name": "{{tag}}", "html_url": "https://github.com/Bojacnik/URL-Shortener-app/releases/tag/{{tag}}", "draft": false, "prerelease": {{(prerelease ? "true" : "false")}} }""");

    [Theory]
    [InlineData("v2.3.0", "2.2.2", true)]
    [InlineData("v2.2.2", "2.2.2", false)]
    [InlineData("v2.2.1", "2.2.2", false)]
    [InlineData("2.10.0", "2.9.9", true)]
    [InlineData("v3.0.0-beta.1", "2.9.0", true)]
    public async Task Compares_versions(string tag, string current, bool expectUpdate)
    {
        var info = await new UpdateChecker(Release(tag).Client()).CheckAsync(Version.Parse(current));
        Assert.Equal(expectUpdate, info is not null);
        if (info is not null) Assert.Contains("/releases/tag/", info.ReleasePage.AbsoluteUri);
    }

    [Fact]
    public async Task Ignores_prereleases_missing_releases_and_errors()
    {
        var current = new Version(1, 0, 0);
        Assert.Null(await new UpdateChecker(Release("v9.0.0", prerelease: true).Client()).CheckAsync(current));
        Assert.Null(await new UpdateChecker(new FakeHttpHandler().On("api.github.com", HttpStatusCode.NotFound, "{}").Client()).CheckAsync(current));
        Assert.Null(await new UpdateChecker(new FakeHttpHandler().On("api.github.com", HttpStatusCode.OK, "<html>").Client()).CheckAsync(current));
        Assert.Null(await new UpdateChecker(new FakeHttpHandler().Throws("api.github.com", new HttpRequestException("offline")).Client()).CheckAsync(current));
    }

    [Fact]
    public void Checks_at_most_daily()
    {
        var now = DateTimeOffset.Now;
        Assert.True(UpdateChecker.IsDue(null, now));
        Assert.False(UpdateChecker.IsDue(now.AddHours(-3), now));
        Assert.True(UpdateChecker.IsDue(now.AddDays(-2), now));
    }
}
