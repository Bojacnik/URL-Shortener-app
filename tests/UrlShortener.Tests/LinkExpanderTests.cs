using System.Net;
using UrlShortener.Core;

namespace UrlShortener.Tests;

public class LinkExpanderTests
{
    private static HttpResponseMessage Redirect(string location, HttpStatusCode code = HttpStatusCode.MovedPermanently)
    {
        var r = new HttpResponseMessage(code);
        r.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return r;
    }

    private static HttpResponseMessage Ok(string body = "", string mediaType = "text/plain") =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType) };

    [Fact]
    public async Task Follows_a_redirect_chain_to_the_end()
    {
        var http = new FakeHttpHandler()
            .On("bit.ly", _ => Redirect("https://www.example.com/start"))
            .On("www.example.com", req => req.RequestUri!.AbsolutePath == "/start"
                ? Redirect("/final?x=1", HttpStatusCode.Found)    // relative Location
                : Ok());
        var result = await new LinkExpander(http.Client()).ExpandAsync(new Uri("https://bit.ly/abc"));

        Assert.Equal(ExpandStop.Done, result.Stop);
        Assert.Equal("https://www.example.com/final?x=1", result.Final.AbsoluteUri);
        Assert.Equal(2, result.Redirects);
        Assert.All(http.Requests, _ => { });
    }

    [Fact]
    public async Task Uses_head_requests_and_falls_back_to_get()
    {
        var methods = new List<HttpMethod>();
        var http = new FakeHttpHandler().On("t.co", req =>
        {
            methods.Add(req.Method);
            return req.Method == HttpMethod.Head
                ? new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
                : Redirect("https://example.org/");
        }).On("example.org", _ => Ok());

        var result = await new LinkExpander(http.Client()).ExpandAsync(new Uri("https://t.co/xyz"));

        Assert.Equal([HttpMethod.Head, HttpMethod.Get], methods);
        Assert.Equal("https://example.org/", result.Final.AbsoluteUri);
    }

    [Fact]
    public async Task Understands_meta_refresh_pages()
    {
        var http = new FakeHttpHandler()
            .On("short.example", req => req.Method == HttpMethod.Head
                ? new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
                : Ok("""<html><head><meta http-equiv="refresh" content="0; URL='https://target.example/page?a=1&amp;b=2'"></head></html>""", "text/html"))
            .On("target.example", _ => Ok());

        var result = await new LinkExpander(http.Client()).ExpandAsync(new Uri("https://short.example/q"));

        Assert.Equal("https://target.example/page?a=1&b=2", result.Final.AbsoluteUri);
    }

    [Fact]
    public async Task Uses_the_isgd_api_for_isgd_and_vgd()
    {
        var http = new FakeHttpHandler()
            .On("v.gd", req => req.RequestUri!.AbsolutePath == "/forward.php"
                ? Ok("""{ "url": "https://github.com/Bojacnik" }""")
                : Ok("<html>preview page</html>", "text/html"))
            .On("github.com", _ => Ok());

        var result = await new LinkExpander(http.Client()).ExpandAsync(new Uri("https://v.gd/abc123"));

        Assert.Equal("https://github.com/Bojacnik", result.Final.AbsoluteUri);
        Assert.Contains(http.Requests, r => r.Query.Contains("shorturl=abc123"));
    }

    [Fact]
    public async Task Stops_before_contacting_a_private_address()
    {
        var http = new FakeHttpHandler().On("evil.example", _ => Redirect("http://192.168.1.1/admin"));
        var result = await new LinkExpander(http.Client()).ExpandAsync(new Uri("https://evil.example/x"));

        Assert.Equal(ExpandStop.PrivateAddress, result.Stop);
        Assert.Equal("http://192.168.1.1/admin", result.Final.AbsoluteUri);
        Assert.DoesNotContain(http.Requests, r => r.Host == "192.168.1.1");
    }

    [Fact]
    public async Task Detects_loops_and_too_many_redirects()
    {
        var loop = new FakeHttpHandler()
            .On("a.example", _ => Redirect("https://b.example/"))
            .On("b.example", _ => Redirect("https://a.example/"));
        Assert.Equal(ExpandStop.Loop, (await new LinkExpander(loop.Client()).ExpandAsync(new Uri("https://a.example/"))).Stop);

        var n = 0;
        var endless = new FakeHttpHandler().On("deep.example", _ => Redirect($"https://deep.example/{++n}"));
        var result = await new LinkExpander(endless.Client()).ExpandAsync(new Uri("https://deep.example/0"));
        Assert.Equal(ExpandStop.TooManyRedirects, result.Stop);
        Assert.Equal(LinkExpander.MaxRedirects + 1, result.Redirects);
    }

    [Fact]
    public async Task Ignores_non_http_redirects()
    {
        var http = new FakeHttpHandler().On("apps.example", _ => Redirect("market://details?id=x"));
        var result = await new LinkExpander(http.Client()).ExpandAsync(new Uri("https://apps.example/go"));

        Assert.Equal(ExpandStop.Done, result.Stop);
        Assert.Equal("https://apps.example/go", result.Final.AbsoluteUri);
    }

    [Fact]
    public async Task First_hop_failure_is_reported_later_failures_keep_the_chain()
    {
        var down = new FakeHttpHandler().Throws("bit.ly", new HttpRequestException("dns"));
        var failed = await new LinkExpander(down.Client()).ExpandAsync(new Uri("https://bit.ly/x"));
        Assert.Equal(ExpandStop.Failed, failed.Stop);
        Assert.False(failed.IsSuccess);

        var destinationDown = new FakeHttpHandler()
            .On("bit.ly", _ => Redirect("https://gone.example/page"))
            .Throws("gone.example", new HttpRequestException("refused"));
        var partial = await new LinkExpander(destinationDown.Client()).ExpandAsync(new Uri("https://bit.ly/x"));
        Assert.True(partial.IsSuccess);
        Assert.Equal("https://gone.example/page", partial.Final.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://bit.ly/3abcDEF", true)]
    [InlineData("is.gd/abc", true)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]
    [InlineData("https://bit.ly/", false)]
    [InlineData("https://github.com/Bojacnik", false)]
    [InlineData("not a link", false)]
    public void Recognises_known_short_links(string text, bool expected) =>
        Assert.Equal(expected, ShortLinks.IsKnownShortLink(text));
}
