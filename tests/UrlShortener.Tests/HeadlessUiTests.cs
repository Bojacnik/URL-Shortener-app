using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using UrlShortener.Core;
using UrlShortener.UI.Localization;
using UrlShortener.UI.ViewModels;
using UrlShortener.UI.Views;

[assembly: AvaloniaTestApplication(typeof(UrlShortener.Tests.TestAppBuilder))]

namespace UrlShortener.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<UrlShortener.UI.App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Runs the real views with real XAML in a headless Avalonia instance.</summary>
public class HeadlessUiTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly List<Window> _open = [];

    private void Open(Window window)
    {
        window.Show();
        _open.Add(window);
    }

    public void Dispose()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var w in _open) w.Close();
            if (Application.Current is { } app) app.RequestedThemeVariant = ThemeVariant.Default;
            Loc.Instance.SetLanguage("en");
        });
        _dir.Dispose();
    }

    private MainViewModel CreateVm(FakeHttpHandler http, FakePlatform? platform = null) =>
        new(ShortenerService.CreateDefault(http.Client()), new AppStorage(_dir.Path), platform ?? new FakePlatform())
        {
            FeedbackDuration = TimeSpan.FromSeconds(30),
        };

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Let entrance animations finish before taking a picture.</summary>
    private static async Task Settle()
    {
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [AvaloniaFact]
    public async Task Typing_a_url_and_pressing_enter_shows_the_short_link()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/demo1" }""");
        var vm = CreateVm(http);
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        Open(window);

        var box = window.MainView.FindControl<TextBox>("UrlBox")!;
        box.Focus();
        window.KeyTextInput("github.com/Bojacnik/URL-Shortener-app");
        Assert.Equal("github.com/Bojacnik/URL-Shortener-app", vm.InputUrl);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await WaitFor(() => vm.ShortUrl is not null);

        Assert.Equal("https://is.gd/demo1", vm.ShortUrl);
        Assert.True(window.MainView.FindControl<Border>("ResultCard")!.IsVisible);
        Assert.Single(window.MainView.FindControl<ItemsControl>("HistoryList")!.Items);
    }

    [AvaloniaFact]
    public async Task Screenshots_desktop_light_and_dark()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/avalonia" }""");
        var storage = new AppStorage(_dir.Path);
        await storage.SaveHistoryAsync(
        [
            new HistoryEntry("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://tinyurl.com/yt-classic", "tinyurl", DateTimeOffset.Now.AddHours(-3)),
            new HistoryEntry("https://docs.avaloniaui.net/docs/welcome", "https://da.gd/avdocs", "da.gd", DateTimeOffset.Now.AddDays(-2)),
        ]);
        var vm = CreateVm(http);
        await vm.InitializeAsync();
        vm.InputUrl = "https://github.com/Bojacnik/URL-Shortener-app";
        await vm.ShortenCommand.ExecuteAsync(null);

        var window = new MainWindow { DataContext = vm, Width = 580, Height = 1180 };
        Open(window);
        await Settle();
        SaveScreenshot(window, "desktop-light.png");

        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        vm.ShowQr = true;
        await Settle();
        SaveScreenshot(window, "desktop-dark-qr.png");
    }

    [AvaloniaFact]
    public async Task Screenshots_phone_sizes()
    {
        var vm = CreateVm(new FakeHttpHandler(), new FakePlatform { CanShare = true });
        await vm.InitializeAsync();

        // Empty start screen on a typical Android phone (412 × 915 dp).
        var phone = new Window { Width = 412, Height = 915, Content = new MainView { DataContext = vm } };
        Open(phone);
        await Settle();
        SaveScreenshot(phone, "phone-start.png");
        phone.Close();

        // Validation and alias on a small phone (360 dp wide).
        vm.InputUrl = "ftp://files.example.com";
        vm.UseAlias = true;
        vm.Alias = "ab";
        var small = new Window { Width = 360, Height = 800, Content = new MainView { DataContext = vm } };
        Open(small);
        await Settle();
        SaveScreenshot(small, "phone-validation.png");
    }

    [AvaloniaFact]
    public async Task Screenshot_phone_result_with_share()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/phone7" }""");
        var vm = CreateVm(http, new FakePlatform { CanShare = true });
        await vm.InitializeAsync();
        await vm.ReceiveSharedTextAsync("Shared from Chrome: https://en.wikipedia.org/wiki/URL_shortening");
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;

        var phone = new Window { Width = 412, Height = 915, Content = new MainView { DataContext = vm } };
        Open(phone);
        await Settle();
        SaveScreenshot(phone, "phone-dark-result.png");
    }

    private static HttpResponseMessage Redirect(string to)
    {
        var r = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        r.Headers.Location = new Uri(to);
        return r;
    }

    [AvaloniaFact]
    public async Task Screenshot_batch_mode()
    {
        var n = 0;
        var http = new FakeHttpHandler().On("is.gd", _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($$"""{ "shorturl": "https://is.gd/team{{++n}}" }""") });
        var vm = CreateVm(http);
        vm.Batch.Spacing = TimeSpan.Zero;
        await vm.InitializeAsync();
        vm.Mode = AppMode.Batch;
        vm.Batch.Input = "https://github.com/Bojacnik/URL-Shortener-app\nhttps://docs.avaloniaui.net/docs/welcome\nnot a link\nhttps://learn.microsoft.com/dotnet/";
        await vm.Batch.ShortenAllCommand.ExecuteAsync(null);

        var window = new MainWindow { DataContext = vm, Width = 580, Height = 1300 };
        Open(window);
        await Settle();
        Assert.True(window.MainView.FindControl<Border>("BatchResults")!.IsVisible);
        Assert.Equal(4, window.MainView.FindControl<ItemsControl>("BatchList")!.ItemCount);
        SaveScreenshot(window, "batch.png");
    }

    [AvaloniaFact]
    public async Task Screenshot_expand_mode()
    {
        var http = new FakeHttpHandler()
            .On("bit.ly", _ => Redirect("https://t.co/redirect"))
            .On("t.co", _ => Redirect("https://www.example.com/promo/spring-sale?utm_source=newsletter"))
            .On("www.example.com", _ => new HttpResponseMessage(HttpStatusCode.OK));
        var vm = new MainViewModel(ShortenerService.CreateDefault(http.Client()), new AppStorage(_dir.Path), new FakePlatform(), new LinkExpander(http.Client()));
        await vm.InitializeAsync();
        vm.Mode = AppMode.Expand;
        vm.Expand.Input = "https://bit.ly/4spring";
        await vm.Expand.ExpandCommand.ExecuteAsync(null);
        vm.Expand.ShowPath = true;

        var window = new MainWindow { DataContext = vm, Width = 580, Height = 1000 };
        Open(window);
        await Settle();
        Assert.True(window.MainView.FindControl<Border>("ExpandResult")!.IsVisible);
        SaveScreenshot(window, "expand.png");
    }

    [AvaloniaFact]
    public async Task Screenshot_quick_popup()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/quick1" }""");
        var vm = new QuickViewModel(ShortenerService.CreateDefault(http.Client()), new AppStorage(_dir.Path),
            new FakePlatform { Clipboard = "https://github.com/Bojacnik" })
        { DoneDelay = TimeSpan.FromSeconds(30) };
        _ = vm.RunAsync();
        await WaitFor(() => vm.IsDone);

        var window = new QuickWindow { DataContext = vm };
        Open(window);
        await Settle();
        SaveScreenshot(window, "quick.png");
    }

    [AvaloniaTheory]
    [InlineData("cs")]
    [InlineData("ja")]
    [InlineData("ar")]
    [InlineData("hi")]
    [InlineData("de")]
    public async Task Screenshot_languages(string language)
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/lang" }""");
        var storage = new AppStorage(_dir.Path);
        await storage.SaveHistoryAsync([new HistoryEntry("https://www.wikipedia.org/", "https://da.gd/wiki", "da.gd", DateTimeOffset.Now.AddHours(-5))]);
        await storage.SaveSettingsAsync(new AppSettings { Language = language });
        var vm = CreateVm(http, new FakePlatform { CanShare = true });
        await vm.InitializeAsync();
        vm.InputUrl = "https://github.com/Bojacnik/URL-Shortener-app";
        await vm.ShortenCommand.ExecuteAsync(null);

        Assert.Equal(language, Loc.Instance.Language.Code);
        var phone = new Window { Width = 412, Height = 1050, Content = new MainView { DataContext = vm } };
        Open(phone);
        await Settle();
        SaveScreenshot(phone, $"lang-{language}.png");
    }

    [AvaloniaFact]
    public async Task Screenshot_new_features()
    {
        var http = new FakeHttpHandler()
            .On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/v23" }""")
            .On("api.github.com", HttpStatusCode.OK, """{ "tag_name": "v9.9.0", "html_url": "https://github.com/x/y/releases/tag/v9.9.0", "draft": false, "prerelease": false }""");
        var storage = new AppStorage(_dir.Path);
        await storage.SaveHistoryAsync(
        [
            new HistoryEntry("https://www.wikipedia.org/", "https://da.gd/wiki", "da.gd", DateTimeOffset.Now.AddDays(-3), Pinned: true),
            new HistoryEntry("https://news.ycombinator.com/", "https://tinyurl.com/hn-front", "tinyurl", DateTimeOffset.Now.AddHours(-2)),
        ]);
        var platform = new FakePlatform { Clipboard = "https://github.com/AvaloniaUI/Avalonia/releases" };
        var vm = new MainViewModel(ShortenerService.CreateDefault(http.Client()), storage, platform, updates: new UpdateChecker(http.Client()))
        {
            FeedbackDuration = TimeSpan.FromSeconds(30),
        };
        await vm.InitializeAsync();
        await vm.CheckForUpdatesAsync(force: true);
        vm.InputUrl = "https://github.com/Bojacnik/URL-Shortener-app";
        await vm.ShortenCommand.ExecuteAsync(null);
        vm.ShowQr = true;
        platform.Clipboard = "https://docs.avaloniaui.net/docs/guides/platforms/android";
        await vm.CheckClipboardAsync();

        var window = new MainWindow { DataContext = vm, Width = 580, Height = 1500 };
        Open(window);
        await Settle();
        Assert.True(vm.HasUpdate);
        Assert.True(vm.HasClipboardSuggestion);
        Assert.True(vm.History[0].Pinned);
        SaveScreenshot(window, "new-features.png");
    }

    private static void SaveScreenshot(Window window, string name)
    {
        var dir = Environment.GetEnvironmentVariable("SCREENSHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name));
    }
}
