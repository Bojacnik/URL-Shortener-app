using Avalonia;
using UrlShortener.Core;
using UrlShortener.UI;

namespace UrlShortener.Desktop;

internal sealed class Program
{
    // Don't use Avalonia or SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["shorten", ..])
            return ShortenFromCommandLine(args[1..]).GetAwaiter().GetResult();

        if (args is ["--help" or "-h"])
        {
            Console.WriteLine("""
                URL Shortener

                  UrlShortener                     open the app
                  UrlShortener --quick             shorten the link on the clipboard and copy the result
                                                   (give this a keyboard shortcut in your system settings)
                  UrlShortener shorten <url> [--service is.gd|tinyurl|da.gd|v.gd] [--alias name]
                                                   print a short link (for scripts and terminals)
                """);
            return 0;
        }

        App.QuickMode = args.Contains("--quick");
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    /// <summary>`UrlShortener shorten https://… [--service id] [--alias name]`: prints the short link, exit code 0/1.</summary>
    private static async Task<int> ShortenFromCommandLine(string[] args)
    {
        string? url = null, service = null, alias = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--service" when i + 1 < args.Length: service = args[++i]; break;
                case "--alias" when i + 1 < args.Length: alias = args[++i]; break;
                default: url ??= args[i]; break;
            }
        }

        if (url is null)
        {
            Console.Error.WriteLine("Usage: UrlShortener shorten <url> [--service is.gd|tinyurl|da.gd|v.gd] [--alias name]");
            return 2;
        }

        using var http = ShortenerService.CreateHttpClient();
        var settings = await new AppStorage().LoadSettingsAsync();
        var outcome = await ShortenerService.CreateDefault(http)
            .ShortenAsync(url, service ?? settings.PreferredProviderId, alias, settings.AutoFallback);

        if (outcome.IsSuccess)
        {
            Console.WriteLine(outcome.Final.ShortUrl);
            return 0;
        }
        Console.Error.WriteLine(outcome.Final.Error);
        return 1;
    }
}
