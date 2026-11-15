using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UrlShortener.App.Services;
using UrlShortener.App.ViewModels;
using UrlShortener.App.Views;
using UrlShortener.Core;

namespace UrlShortener.App;

public partial class App : Application
{
    private HttpClient? _http;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _http = ShortenerService.CreateHttpClient();
            var window = new MainWindow();
            var viewModel = new MainViewModel(
                ShortenerService.CreateDefault(_http),
                new AppStorage(),
                new AvaloniaPlatformServices(window));
            window.DataContext = viewModel;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _http?.Dispose();

            _ = viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
