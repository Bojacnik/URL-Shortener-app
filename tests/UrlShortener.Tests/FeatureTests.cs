using System.Globalization;
using System.Net;
using UrlShortener.Core;
using UrlShortener.UI.Localization;
using UrlShortener.UI.Services;
using UrlShortener.UI.ViewModels;

// Loc is app-wide state; tests that switch language must not run alongside tests that read English text.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace UrlShortener.Tests;

public class BatchTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakePlatform _platform = new();
    public void Dispose() => _dir.Dispose();

    private MainViewModel Create(FakeHttpHandler http)
    {
        var vm = new MainViewModel(ShortenerService.CreateDefault(http.Client()), new AppStorage(_dir.Path), _platform)
        {
            FeedbackDuration = TimeSpan.FromMilliseconds(50),
        };
        vm.Batch.Spacing = TimeSpan.Zero;
        return vm;
    }

    [Fact]
    public async Task Shortens_each_line_and_reports_failures()
    {
        var n = 0;
        var http = new FakeHttpHandler().On("is.gd", _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($$"""{ "shorturl": "https://is.gd/b{{++n}}" }""") });
        var vm = Create(http);
        vm.Batch.Input = """
            https://example.com/one
            Look: https://example.com/two

            ftp://nope.example
            https://example.com/one
            """;

        Assert.Equal(3, vm.Batch.LinkCount); // blank line and duplicate dropped
        await vm.Batch.ShortenAllCommand.ExecuteAsync(null);

        Assert.Equal(["https://is.gd/b1", "https://is.gd/b2", null], vm.Batch.Results.Select(r => r.ShortUrl));
        Assert.True(vm.Batch.Results[2].IsFailed);
        Assert.Equal("Only http:// and https:// links work.", vm.Batch.Results[2].ErrorText);
        Assert.Equal("Shortened: 2 · Failed: 1", vm.Batch.SummaryText);
        Assert.Equal(2, vm.HistoryCount);
        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task Copies_all_or_as_a_table()
    {
        var http = new FakeHttpHandler().On("is.gd", req =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(req.RequestUri!.Query.Contains("one")
                    ? """{ "shorturl": "https://is.gd/1" }""" : """{ "shorturl": "https://is.gd/2" }"""),
            });
        var vm = Create(http);
        vm.Batch.Input = "https://example.com/one\nhttps://example.com/two";
        await vm.Batch.ShortenAllCommand.ExecuteAsync(null);

        await vm.Batch.CopyAllCommand.ExecuteAsync(null);
        Assert.Equal($"https://is.gd/1{Environment.NewLine}https://is.gd/2", _platform.Clipboard);

        await vm.Batch.CopyTableCommand.ExecuteAsync(null);
        Assert.Equal($"https://example.com/one\thttps://is.gd/1{Environment.NewLine}https://example.com/two\thttps://is.gd/2", _platform.Clipboard);
    }

    [Fact]
    public void Limits_the_number_of_links()
    {
        var vm = Create(new FakeHttpHandler());
        vm.Batch.Input = string.Join('\n', Enumerable.Range(0, BatchViewModel.MaxLinks + 5).Select(i => $"https://example.com/{i}"));
        Assert.True(vm.Batch.OverLimit);
    }
}

public class ExpandTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakePlatform _platform = new();
    public void Dispose() => _dir.Dispose();

    private static HttpResponseMessage Redirect(string to)
    {
        var r = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        r.Headers.Location = new Uri(to);
        return r;
    }

    private MainViewModel Create(FakeHttpHandler http) =>
        new(ShortenerService.CreateDefault(http.Client()), new AppStorage(_dir.Path), _platform, new LinkExpander(http.Client()))
        {
            FeedbackDuration = TimeSpan.FromMilliseconds(50),
        };

    [Fact]
    public async Task Shows_where_a_short_link_goes()
    {
        var http = new FakeHttpHandler()
            .On("bit.ly", _ => Redirect("https://www.example.com/article"))
            .On("www.example.com", _ => new HttpResponseMessage(HttpStatusCode.OK));
        var vm = Create(http);
        vm.Expand.Input = "bit.ly/abc";
        await vm.Expand.ExpandCommand.ExecuteAsync(null);

        Assert.True(vm.Expand.HasResult);
        Assert.Equal("example.com", vm.Expand.FinalHost);
        Assert.Equal("https://www.example.com/article", vm.Expand.FinalUrl);
        Assert.Equal("Redirects followed: 1", vm.Expand.RedirectsText);
        Assert.False(vm.Expand.HasWarning);

        vm.Expand.ShortenThisCommand.Execute(null);
        Assert.Equal(AppMode.Shorten, vm.Mode);
        Assert.Equal("https://www.example.com/article", vm.InputUrl);
    }

    [Fact]
    public async Task Warns_about_private_destinations()
    {
        var http = new FakeHttpHandler().On("t.co", _ => Redirect("http://10.0.0.1/router"));
        var vm = Create(http);
        vm.Expand.Input = "https://t.co/x";
        await vm.Expand.ExpandCommand.ExecuteAsync(null);

        Assert.True(vm.Expand.HasWarning);
        Assert.Contains("private", vm.Expand.WarningText);
    }

    [Fact]
    public async Task A_short_link_in_the_shorten_box_offers_to_expand()
    {
        var http = new FakeHttpHandler()
            .On("bit.ly", _ => Redirect("https://example.org/"))
            .On("example.org", _ => new HttpResponseMessage(HttpStatusCode.OK));
        var vm = Create(http);
        vm.InputUrl = "https://bit.ly/3xyz";
        Assert.True(vm.InputLooksShort);

        await vm.ExpandInputCommand.ExecuteAsync(null);
        Assert.Equal(AppMode.Expand, vm.Mode);
        Assert.Equal("https://example.org/", vm.Expand.FinalUrl);
    }

    [Fact]
    public async Task Shared_short_links_are_expanded_not_shortened()
    {
        var http = new FakeHttpHandler()
            .On("youtu.be", _ => Redirect("https://www.youtube.com/watch?v=abc"))
            .On("www.youtube.com", _ => new HttpResponseMessage(HttpStatusCode.OK));
        var vm = Create(http);
        await vm.ReceiveSharedTextAsync("Watch this https://youtu.be/abc");

        Assert.Equal(AppMode.Expand, vm.Mode);
        Assert.Equal("youtube.com", vm.Expand.FinalHost);
        Assert.DoesNotContain(http.Requests, r => r.Host == "is.gd");
    }

    [Fact]
    public async Task Unreachable_links_show_an_error()
    {
        var http = new FakeHttpHandler().Throws("bit.ly", new HttpRequestException("dns"));
        var vm = Create(http);
        vm.Expand.Input = "https://bit.ly/q";
        await vm.Expand.ExpandCommand.ExecuteAsync(null);

        Assert.False(vm.Expand.HasResult);
        Assert.True(vm.Expand.HasError);
    }
}

public class QuickModeTests : IDisposable
{
    private readonly TempDir _dir = new();
    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task Shortens_the_clipboard_and_saves_history()
    {
        var http = new FakeHttpHandler().On("tinyurl.com", HttpStatusCode.OK, "https://tinyurl.com/q1");
        var storage = new AppStorage(_dir.Path);
        await storage.SaveSettingsAsync(new AppSettings { PreferredProviderId = "tinyurl" });
        var platform = new FakePlatform { Clipboard = "copied from a chat: https://example.com/long/page" };
        var vm = new QuickViewModel(ShortenerService.CreateDefault(http.Client()), storage, platform) { DoneDelay = TimeSpan.Zero };
        var finished = false;
        vm.Finished += (_, _) => finished = true;

        await vm.RunAsync();

        Assert.True(vm.IsDone);
        Assert.Equal("https://tinyurl.com/q1", platform.Clipboard);
        Assert.Equal("tinyurl.com/q1", vm.Detail);
        Assert.True(finished);
        Assert.Equal("https://example.com/long/page", Assert.Single(await storage.LoadHistoryAsync()).LongUrl);
    }

    [Fact]
    public async Task Says_so_when_the_clipboard_has_no_link()
    {
        var platform = new FakePlatform { Clipboard = "just some words" };
        var vm = new QuickViewModel(ShortenerService.CreateDefault(new FakeHttpHandler().Client()), new AppStorage(_dir.Path), platform)
        {
            FailDelay = TimeSpan.Zero,
        };
        await vm.RunAsync();

        Assert.True(vm.IsFailed);
        Assert.Equal("There's no link on the clipboard", vm.Title);
        Assert.Equal("just some words", platform.Clipboard);
    }
}

public class LocalizationTests : IDisposable
{
    public void Dispose() => Loc.Instance.SetLanguage("en");

    private static Dictionary<string, string> Table(string code) => Loc.Load(code) ?? throw new Xunit.Sdk.XunitException($"Missing i18n/{code}.json");

    public static TheoryData<string> AllLanguages => new(Loc.Languages.Select(l => l.Code));

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Every_language_has_every_string(string code)
    {
        var english = Table("en");
        var table = Table(code);
        Assert.Empty(english.Keys.Except(table.Keys));        // nothing missing
        Assert.Empty(table.Keys.Except(english.Keys));        // nothing stale
        Assert.DoesNotContain(table, kv => string.IsNullOrWhiteSpace(kv.Value) && !kv.Key.StartsWith("Hero.Title"));
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Placeholders_match_english(string code)
    {
        var english = Table("en");
        foreach (var (key, value) in Table(code))
        {
            var expected = Placeholders(english[key]);
            Assert.True(expected.SetEquals(Placeholders(value)), $"{code}: '{key}' uses {{{string.Join(",", Placeholders(value))}}} but English uses {{{string.Join(",", expected)}}}");
            // Must also format without throwing.
            _ = string.Format(CultureInfo.InvariantCulture, value, "a", "b", "c");
        }
    }

    private static HashSet<string> Placeholders(string s) =>
        System.Text.RegularExpressions.Regex.Matches(s, @"\{(\d+)\}").Select(m => m.Groups[1].Value).ToHashSet();

    [Theory]
    [InlineData("cs-CZ", "cs")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("zh-TW", "zh-Hans")]
    [InlineData("es-MX", "es")]
    [InlineData("sv-SE", "en")]
    public void Picks_the_closest_system_language(string system, string expected) =>
        Assert.Equal(expected, Loc.Resolve(Loc.SystemCode, new CultureInfo(system)).Code);

    // Runs on the UI thread, like a real language change from the settings menu.
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task Switching_language_updates_the_ui_text()
    {
        using var dir = new TempDir();
        var vm = new MainViewModel(ShortenerService.CreateDefault(new FakeHttpHandler().Client()), new AppStorage(dir.Path), new FakePlatform());
        await vm.InitializeAsync();
        vm.InputUrl = "ftp://x.com";
        var english = vm.InputHint;

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.Language = "cs";

        Assert.Equal("cs", Loc.Instance.Language.Code);
        Assert.NotEqual(english, vm.InputHint);
        Assert.Contains(string.Empty, changed); // "refresh everything" was raised
        Assert.Equal(Loc.Instance["Mode.Batch"], vm.ModeChoices[1].Label);

        vm.Language = "ar";
        Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, vm.FlowDirection);
        vm.Language = "en";
        Assert.Equal(Avalonia.Media.FlowDirection.LeftToRight, vm.FlowDirection);
    }
}

public class HistoryFeatureTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakePlatform _platform = new();
    public void Dispose() => _dir.Dispose();

    private async Task<(MainViewModel vm, AppStorage storage)> Create(params HistoryEntry[] history)
    {
        var storage = new AppStorage(_dir.Path);
        await storage.SaveHistoryAsync(history);
        var vm = new MainViewModel(ShortenerService.CreateDefault(new FakeHttpHandler().Client()), storage, _platform)
        {
            FeedbackDuration = TimeSpan.FromMilliseconds(50),
        };
        await vm.InitializeAsync();
        return (vm, storage);
    }

    private static HistoryEntry Entry(string code, int hoursAgo, bool pinned = false) =>
        new($"https://example.com/{code}", $"https://is.gd/{code}", "is.gd", DateTimeOffset.Now.AddHours(-hoursAgo), pinned);

    [Fact]
    public async Task Pinned_links_stay_on_top_and_survive_clear_all()
    {
        var (vm, storage) = await Create(Entry("new", 1), Entry("old", 50));
        vm.TogglePinCommand.Execute(vm.History.Single(h => h.ShortUrl.EndsWith("old")));
        Assert.Equal("https://is.gd/old", vm.History[0].ShortUrl);
        Assert.True(vm.History[0].Pinned);

        vm.ClearHistoryCommand.Execute(null);
        Assert.Equal("https://is.gd/old", Assert.Single(vm.History).ShortUrl);
        await Task.Delay(100);
        Assert.True(Assert.Single(await storage.LoadHistoryAsync()).Pinned);
    }

    [Fact]
    public async Task Export_and_import_history()
    {
        var (vm, _) = await Create(Entry("a", 1), Entry("b", 2, pinned: true));
        await vm.ExportHistoryCommand.ExecuteAsync(null);
        var (name, bytes) = Assert.Single(_platform.SavedFiles);
        Assert.EndsWith(".csv", name);
        var csv = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("https://is.gd/a", csv);

        // Import into an empty app: everything comes back, pin included.
        using var other = new TempDir();
        var fresh = new MainViewModel(ShortenerService.CreateDefault(new FakeHttpHandler().Client()), new AppStorage(other.Path), _platform);
        await fresh.InitializeAsync();
        _platform.FileToOpen = csv.TrimStart('﻿');
        await fresh.ImportHistoryCommand.ExecuteAsync(null);
        Assert.Equal(["https://is.gd/b", "https://is.gd/a"], fresh.History.Select(h => h.ShortUrl));
        Assert.Equal("Links imported: 2", fresh.ToastMessage);

        _platform.FileToOpen = "this,is\nnot,history";
        await fresh.ImportHistoryCommand.ExecuteAsync(null);
        Assert.Equal("That file couldn't be read as link history.", fresh.ToastMessage);
        Assert.Equal(2, fresh.HistoryCount);
    }

    [Fact]
    public async Task Saves_the_qr_code_as_png()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/qr1" }""");
        var vm = new MainViewModel(ShortenerService.CreateDefault(http.Client()), new AppStorage(_dir.Path), _platform);
        vm.InputUrl = "example.com";
        await vm.ShortenCommand.ExecuteAsync(null);
        await vm.SaveQrCommand.ExecuteAsync(null);

        var (name, png) = Assert.Single(_platform.SavedFiles);
        Assert.Equal("qr-is-gd-qr1.png", name);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png.Take(4));
    }

    [Fact]
    public async Task Offers_a_long_link_from_the_clipboard_once()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/clip" }""");
        var vm = new MainViewModel(ShortenerService.CreateDefault(http.Client()), new AppStorage(_dir.Path), _platform);
        await vm.InitializeAsync();

        _platform.Clipboard = "see https://example.com/some/long/article?id=42";
        await vm.CheckClipboardAsync();
        Assert.Equal("https://example.com/some/long/article?id=42", vm.ClipboardSuggestion);

        vm.DismissClipboardSuggestionCommand.Execute(null);
        await vm.CheckClipboardAsync();           // same link again: don't nag
        Assert.False(vm.HasClipboardSuggestion);

        _platform.Clipboard = "https://bit.ly/abc"; // already short: not offered
        await vm.CheckClipboardAsync();
        Assert.False(vm.HasClipboardSuggestion);

        _platform.Clipboard = "https://example.com/another";
        await vm.CheckClipboardAsync();
        await vm.UseClipboardSuggestionCommand.ExecuteAsync(null);
        Assert.Equal("https://is.gd/clip", vm.ShortUrl);
        Assert.False(vm.HasClipboardSuggestion);

        await vm.CheckClipboardAsync();           // it's in history now
        Assert.False(vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task Announces_updates_and_remembers_later()
    {
        var http = new FakeHttpHandler().On("api.github.com", HttpStatusCode.OK,
            """{ "tag_name": "v99.0.0", "html_url": "https://github.com/Bojacnik/URL-Shortener-app/releases/tag/v99.0.0", "draft": false, "prerelease": false }""");
        var storage = new AppStorage(_dir.Path);
        var vm = new MainViewModel(ShortenerService.CreateDefault(http.Client()), storage, _platform, updates: new UpdateChecker(http.Client()));
        await vm.InitializeAsync();
        await vm.CheckForUpdatesAsync(force: true);

        Assert.True(vm.HasUpdate);
        Assert.Equal("Version 99.0.0 is available.", vm.UpdateText);

        await vm.OpenUpdateCommand.ExecuteAsync(null);
        Assert.Contains("https://github.com/Bojacnik/URL-Shortener-app/releases/tag/v99.0.0", _platform.Opened);

        vm.DismissUpdateCommand.Execute(null);
        await Task.Delay(100);
        var again = new MainViewModel(ShortenerService.CreateDefault(http.Client()), storage, _platform, updates: new UpdateChecker(http.Client()));
        await again.InitializeAsync();
        await again.CheckForUpdatesAsync(force: true);
        Assert.False(again.HasUpdate);              // "Later" sticks for this version
        Assert.Equal("v99.0.0", (await storage.LoadSettingsAsync()).DismissedVersion);
    }
}

/// <summary>The Android labels and the Linux menu entry are generated from the translation tables; keep them in sync.</summary>
public class GeneratedFilesTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "UrlShortener.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }

    private static Dictionary<string, string> Table(string code) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(Root, "src/UrlShortener.UI/i18n", code + ".json")))!;

    [Theory]
    [InlineData("en", "values")]
    [InlineData("cs", "values-cs")]
    [InlineData("pt-BR", "values-pt-rBR")]
    [InlineData("zh-Hans", "values-b+zh+Hans")]
    [InlineData("id", "values-in")]
    [InlineData("ar", "values-ar")]
    public void Android_labels_match_translations(string code, string folder)
    {
        var file = Path.Combine(Root, "src/UrlShortener.Android/Resources", folder, "strings.xml");
        var strings = System.Xml.Linq.XDocument.Load(file).Root!.Elements("string")
            .ToDictionary(e => (string)e.Attribute("name")!, e => e.Value.Replace("\\'", "'").Replace("\\\"", "\""));
        var table = Table(code);
        Assert.Equal(table["App.Title"], strings["app_name"]);
        Assert.Equal(table["Android.ShortenLink"], strings["shorten_link"]);
        Assert.Equal(table["Android.ShortenClipboard"], strings["shorten_clipboard"]);
        Assert.Equal(table["Android.ShortenClipboardLong"], strings["shorten_clipboard_long"]);
    }

    [Fact]
    public void Every_language_has_android_labels() =>
        Assert.Equal(Loc.Languages.Count,
            Directory.GetFiles(Path.Combine(Root, "src/UrlShortener.Android/Resources"), "strings.xml", SearchOption.AllDirectories).Length);

    [Fact]
    public void Linux_menu_entry_matches_translations()
    {
        var desktop = File.ReadAllLines(Path.Combine(Root, "packaging/linux/url-shortener.desktop"));
        Assert.Contains($"Name={Table("en")["App.Title"]}", desktop);
        Assert.Contains($"Name[cs]={Table("cs")["App.Title"]}", desktop);
        Assert.Contains($"Name[ja]={Table("ja")["Android.ShortenClipboard"]}", desktop);
        Assert.Contains("Exec=UrlShortener --quick", desktop);
    }
}
