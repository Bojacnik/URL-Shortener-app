using System.Net;
using UrlShortener.App.ViewModels;
using UrlShortener.Core;

namespace UrlShortener.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakePlatform _platform = new();

    public void Dispose() => _dir.Dispose();

    private (MainViewModel vm, AppStorage storage) Create(FakeHttpHandler http)
    {
        var storage = new AppStorage(_dir.Path);
        return (new MainViewModel(ShortenerService.CreateDefault(http.Client()), storage, _platform), storage);
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
        Assert.Equal(StatusKind.Success, vm.Status);
        Assert.Single(vm.History);

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

        Assert.Equal(StatusKind.Error, vm.Status);
        Assert.Contains("valid URL", vm.StatusMessage);
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
