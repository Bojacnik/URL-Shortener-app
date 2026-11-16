using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using UrlShortener.Core;
using UrlShortener.UI.Services;
using UrlShortener.UI.ViewModels;
using UrlShortener.UI.Views;

namespace UrlShortener.UI;

public partial class App : Application
{
    private static string? _pendingSharedText;

    private HttpClient? _http;
    private HttpClient? _expanderHttp;
    private MainViewModel? _viewModel;
    private Control? _currentView;
    private Task? _initialized;

    /// <summary>Set by platforms that have a system share sheet (Android).</summary>
    public static Action<string>? ShareHandler { get; set; }

    /// <summary>Desktop "--quick": show only the clipboard pop-up instead of the main window.</summary>
    public static bool QuickMode { get; set; }

    /// <summary>
    /// Called by the Android activity when another app shares text to us.
    /// Safe to call before the app has finished starting; the text is kept until it is ready.
    /// </summary>
    public static void ReceiveSharedText(string text) => Dispatcher.UIThread.Post(async () =>
    {
        if (Current is App { _viewModel: { } vm, _initialized: { } ready })
        {
            await ready;
            await vm.ReceiveSharedTextAsync(text);
        }
        else
        {
            _pendingSharedText = text;
        }
    });

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _http = ShortenerService.CreateHttpClient();
        var shortener = ShortenerService.CreateDefault(_http);
        var storage = new AppStorage();
        var platform = new AvaloniaPlatformServices(() => TopLevel.GetTopLevel(_currentView));

        if (QuickMode && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime quick)
        {
            var popup = new QuickWindow { DataContext = new QuickViewModel(shortener, storage, platform) };
            _currentView = popup;
            quick.MainWindow = popup;
            quick.Exit += (_, _) => _http?.Dispose();
            base.OnFrameworkInitializationCompleted();
            return;
        }

        _expanderHttp = LinkExpander.CreateHttpClient();
        _viewModel = new MainViewModel(shortener, storage, platform, new LinkExpander(_expanderHttp));

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                var window = new MainWindow { DataContext = _viewModel };
                _currentView = window.MainView;
                desktop.MainWindow = window;
                desktop.Exit += (_, _) =>
                {
                    _http?.Dispose();
                    _expanderHttp?.Dispose();
                };
                break;

            // Android: the activity can be recreated (rotation, theme change), so a fresh view is made
            // each time, all sharing the one view model.
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => _currentView = new MainView { DataContext = _viewModel };
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = _currentView = new MainView { DataContext = _viewModel };
                break;
        }

        _initialized = InitializeAsync(_viewModel);
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeAsync(MainViewModel vm)
    {
        await vm.InitializeAsync();
        if (Interlocked.Exchange(ref _pendingSharedText, null) is { } shared)
            await vm.ReceiveSharedTextAsync(shared);
    }
}
