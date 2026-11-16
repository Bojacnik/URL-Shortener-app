using System.Globalization;
using System.Net;
using UrlShortener.Core;
using UrlShortener.UI.Localization;
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
