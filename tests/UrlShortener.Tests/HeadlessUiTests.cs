using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using UrlShortener.App.ViewModels;
using UrlShortener.App.Views;
using UrlShortener.Core;

[assembly: AvaloniaTestApplication(typeof(UrlShortener.Tests.TestAppBuilder))]

namespace UrlShortener.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<UrlShortener.App.App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Runs the real window with real XAML in a headless Avalonia instance.</summary>
public class HeadlessUiTests : IDisposable
{
    private readonly TempDir _dir = new();
    public void Dispose() => _dir.Dispose();

    [AvaloniaFact]
    public async Task Typing_a_url_and_pressing_enter_shows_the_short_link()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/demo1" }""");
        var vm = new MainViewModel(ShortenerService.CreateDefault(http.Client()), new AppStorage(_dir.Path), new FakePlatform());
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        var box = window.FindControl<TextBox>("UrlBox")!;
        box.Focus();
        window.KeyTextInput("github.com/Bojacnik/URL-Shortener-app");
        Assert.Equal("github.com/Bojacnik/URL-Shortener-app", vm.InputUrl);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        for (var i = 0; i < 50 && vm.ShortUrl is null; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal("https://is.gd/demo1", vm.ShortUrl);
        Dispatcher.UIThread.RunJobs();
        Assert.Single(window.FindControl<ListBox>("HistoryList")!.Items);

        SaveScreenshot(window, "main-window.png");
    }

    [AvaloniaFact]
    public async Task Dark_theme_renders()
    {
        var vm = new MainViewModel(ShortenerService.CreateDefault(new FakeHttpHandler().Client()), new AppStorage(_dir.Path), new App.Services.AvaloniaPlatformServices(new Window()));
        await vm.InitializeAsync();
        vm.Theme = "Dark";
        vm.InputUrl = "ftp://not-allowed.example";
        vm.UseAlias = true;
        vm.Alias = "abc";
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        SaveScreenshot(window, "dark-validation.png");
        vm.Theme = "System";
    }

    private static void SaveScreenshot(Window window, string name)
    {
        var dir = Environment.GetEnvironmentVariable("SCREENSHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name));
    }
}
