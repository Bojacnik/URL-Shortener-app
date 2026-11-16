using System.Net;
using UrlShortener.Core;
using UrlShortener.UI.ViewModels;

namespace UrlShortener.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakePlatform _platform = new();

    public void Dispose() => _dir.Dispose();

    private (MainViewModel vm, AppStorage storage) Create(FakeHttpHandler http)
    {
        var storage = new AppStorage(_dir.Path);
        var vm = new MainViewModel(ShortenerService.CreateDefault(http.Client()), storage, _platform)
        {
            FeedbackDuration = TimeSpan.FromMilliseconds(150),
        };
        return (vm, storage);
    }

    [Fact]
    public async Task Shorten_shows_result_copies_and_saves_history()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/abc" }""");
        var (vm, storage) = Create(http);
        await vm.InitializeAsync();

        vm.InputUrl = "example.com/long";
        Assert.True(vm.ShortenCommand.CanExecute(null));
        await vm.ShortenCommand.ExecuteAsync(null);

        Assert.Equal("https://is.gd/abc", vm.ShortUrl);
        Assert.Equal("https://example.com/long", vm.ResultLongUrl);
        Assert.Equal("https://is.gd/abc", _platform.Clipboard);
        Assert.NotNull(vm.QrCodePng);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal("is.gd/abc", vm.ShortDisplay);
        Assert.True(vm.JustCopied);
        Assert.Single(vm.History);
        await Task.Delay(300);
        Assert.False(vm.JustCopied); // the "Copied" state wears off

        await Task.Delay(100); // history is saved in the background
        Assert.Single(await storage.LoadHistoryAsync());
    }

    [Fact]
    public async Task Failure_shows_error_and_keeps_history_empty()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "errorcode": 1, "errormessage": "Please enter a valid URL" }""");
        var (vm, _) = Create(http);
        await vm.InitializeAsync();

        vm.InputUrl = "example.com";
        await vm.ShortenCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("is.gd refused this link.", vm.ErrorMessage);
        Assert.Null(vm.ShortUrl);
        Assert.Empty(vm.History);
    }

    [Fact]
    public async Task Live_hints_for_bad_url_and_alias()
    {
        var (vm, _) = Create(new FakeHttpHandler());
        vm.InputUrl = "ftp://x.com";
        Assert.NotNull(vm.InputHint);
        Assert.False(vm.ShortenCommand.CanExecute(null));
        vm.InputUrl = "x.com";
        Assert.Null(vm.InputHint);
        Assert.True(vm.ShortenCommand.CanExecute(null));

        vm.UseAlias = true;
        vm.Alias = "ab";
        Assert.NotNull(vm.AliasHint);
        Assert.False(vm.ShortenCommand.CanExecute(null));
        vm.Alias = "abcde";
        Assert.Null(vm.AliasHint);
        Assert.True(vm.ShortenCommand.CanExecute(null));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Settings_persist_between_runs()
    {
        var http = new FakeHttpHandler();
        var (vm, _) = Create(http);
        await vm.InitializeAsync();
        vm.SelectedProvider = vm.Providers.Single(p => p.Id == "tinyurl");
        vm.AutoFallback = false;
        vm.Theme = "Dark";
        await Task.Delay(100);

        var (again, _) = Create(http);
        await again.InitializeAsync();
        Assert.Equal("tinyurl", again.SelectedProvider.Id);
        Assert.False(again.AutoFallback);
        Assert.Equal("Dark", again.Theme);
        Assert.Equal("Dark", _platform.Theme);
    }

    [Fact]
    public async Task History_filter_remove_and_clear()
    {
        var storage = new AppStorage(_dir.Path);
        await storage.SaveHistoryAsync(
        [
            new HistoryEntry("https://github.com/a", "https://is.gd/gh", "is.gd", DateTimeOffset.Now),
            new HistoryEntry("https://youtube.com/b", "https://tinyurl.com/yt", "tinyurl", DateTimeOffset.Now.AddHours(-1)),
        ]);
        var (vm, _) = Create(new FakeHttpHandler());
        await vm.InitializeAsync();
        Assert.Equal(2, vm.History.Count);

        vm.HistoryFilter = "youtube";
        Assert.Equal("https://tinyurl.com/yt", Assert.Single(vm.History).ShortUrl);

        vm.RemoveHistoryItemCommand.Execute(vm.History[0]);
        Assert.Empty(vm.History);
        vm.HistoryFilter = "";
        Assert.Single(vm.History);

        vm.ClearHistoryCommand.Execute(null);
        Assert.True(vm.HistoryIsEmpty);
    }

    [Fact]
    public async Task Paste_fills_input()
    {
        var (vm, _) = Create(new FakeHttpHandler());
        _platform.Clipboard = "  https://example.com/x \n";
        await vm.PasteCommand.ExecuteAsync(null);
        Assert.Equal("https://example.com/x", vm.InputUrl);
    }

    [Fact]
    public async Task Typing_clears_the_previous_error()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.ServiceUnavailable, "");
        var (vm, _) = Create(http);
        vm.AutoFallback = false;
        vm.InputUrl = "example.com";
        await vm.ShortenCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);

        vm.InputUrl = "example.com/x";
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task Shared_text_is_extracted_and_shortened_immediately()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/shared" }""");
        var (vm, _) = Create(http);
        await vm.InitializeAsync();

        await vm.ReceiveSharedTextAsync("Look at this https://example.com/article?id=7 — so good");

        Assert.Equal("https://example.com/article?id=7", vm.ResultLongUrl);
        Assert.Equal("https://is.gd/shared", vm.ShortUrl);
    }

    [Fact]
    public async Task Share_only_where_the_platform_supports_it()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/s" }""");
        var (vm, _) = Create(http);
        vm.InputUrl = "example.com";
        await vm.ShortenCommand.ExecuteAsync(null);
        Assert.False(vm.ShareResultCommand.CanExecute(null));

        _platform.CanShare = true;
        var (phoneVm, _) = Create(http);
        phoneVm.InputUrl = "example.com";
        await phoneVm.ShortenCommand.ExecuteAsync(null);
        phoneVm.ShareResultCommand.Execute(null);
        Assert.Equal(["https://is.gd/s"], _platform.Shared);
    }

    [Fact]
    public async Task Copying_from_history_shows_a_toast_that_goes_away()
    {
        var storage = new AppStorage(_dir.Path);
        await storage.SaveHistoryAsync([new HistoryEntry("https://github.com/a", "https://is.gd/gh", "is.gd", DateTimeOffset.Now)]);
        var (vm, _) = Create(new FakeHttpHandler());
        await vm.InitializeAsync();

        await vm.CopyHistoryItemCommand.ExecuteAsync(vm.History[0]);
        Assert.Equal("https://is.gd/gh", _platform.Clipboard);
        Assert.True(vm.IsToastVisible);
        Assert.Equal("Copied is.gd/gh", vm.ToastMessage);
        await Task.Delay(300);
        Assert.False(vm.IsToastVisible);
    }

    [Fact]
    public void Version_comes_from_the_build()
    {
        var (vm, _) = Create(new FakeHttpHandler());
        Assert.Matches(@"^\d+\.\d+\.\d+$", vm.Version);
        Assert.Equal(typeof(MainViewModel).Assembly.GetName().Version!.ToString(3), vm.Version);
    }

    [Fact]
    public void History_item_display_helpers()
    {
        var item = new HistoryItemViewModel(new HistoryEntry("https://www.github.com/Bojacnik/", "https://is.gd/x1", "is.gd", DateTimeOffset.Now));
        Assert.Equal("is.gd/x1", item.ShortDisplay);
        Assert.Equal("github.com/Bojacnik", item.LongDisplay);
        Assert.Equal("G", item.Initial);
        Assert.Equal(item.Hue, new HistoryItemViewModel(item.Entry with { ShortUrl = "https://is.gd/x2" }).Hue);
    }

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(5, "5 min ago")]
    [InlineData(180, "3 h ago")]
    [InlineData(60 * 24 * 2, "2 d ago")]
    public void Age_formatting(int minutesAgo, string expected)
    {
        var now = DateTimeOffset.Now;
        Assert.Equal(expected, HistoryItemViewModel.FormatAge(now.AddMinutes(-minutesAgo), now));
    }
}
