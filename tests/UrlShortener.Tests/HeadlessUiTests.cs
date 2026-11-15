using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using UrlShortener.Core;
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
    public void Dispose()
    {
        if (Application.Current is { } app) app.RequestedThemeVariant = ThemeVariant.Default;
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
        window.Show();

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
        window.Show();
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
        phone.Show();
        await Settle();
        SaveScreenshot(phone, "phone-start.png");
        phone.Close();

        // Validation and alias on a small phone (360 dp wide).
        vm.InputUrl = "ftp://files.example.com";
        vm.UseAlias = true;
        vm.Alias = "ab";
        var small = new Window { Width = 360, Height = 800, Content = new MainView { DataContext = vm } };
        small.Show();
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
        phone.Show();
        await Settle();
        SaveScreenshot(phone, "phone-dark-result.png");
    }

    private static void SaveScreenshot(Window window, string name)
    {
        var dir = Environment.GetEnvironmentVariable("SCREENSHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name));
    }
}
