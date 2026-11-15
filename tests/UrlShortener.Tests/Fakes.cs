using System.Net;
using UrlShortener.App.Services;

namespace UrlShortener.Tests;

/// <summary>Answers HTTP requests by host, records every request, never touches the network.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new(StringComparer.OrdinalIgnoreCase);

    public List<Uri> Requests { get; } = [];

    public FakeHttpHandler On(string host, HttpStatusCode status, string body) =>
        On(host, _ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    public FakeHttpHandler On(string host, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes[host] = respond;
        return this;
    }

    public FakeHttpHandler Throws(string host, Exception ex) => On(host, _ => throw ex);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return _routes.TryGetValue(request.RequestUri!.Host, out var respond)
            ? Task.FromResult(respond(request))
            : throw new HttpRequestException("No route for " + request.RequestUri, null, HttpStatusCode.ServiceUnavailable);
    }

    public HttpClient Client() => new(this);
}

internal sealed class FakePlatform : IPlatformServices
{
    public string? Clipboard { get; set; }
    public List<string> Opened { get; } = [];
    public string? Theme { get; private set; }

    public Task CopyTextAsync(string text)
    {
        Clipboard = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetClipboardTextAsync() => Task.FromResult(Clipboard);

    public Task<bool> OpenUrlAsync(string url)
    {
        Opened.Add(url);
        return Task.FromResult(true);
    }

    public void SetTheme(string theme) => Theme = theme;
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "urlshortener-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        // The view model saves in the background, so a write may still be landing; retry briefly.
        for (var attempt = 0; attempt < 10 && Directory.Exists(Path); attempt++)
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { Thread.Sleep(50); }
        }
    }
}
