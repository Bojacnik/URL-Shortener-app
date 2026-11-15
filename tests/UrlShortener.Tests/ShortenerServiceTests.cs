using System.Net;
using UrlShortener.Core;

namespace UrlShortener.Tests;

public class ShortenerServiceTests
{
    private const string IsGdOk = """{ "shorturl": "https://is.gd/ok" }""";

    [Fact]
    public async Task Uses_preferred_provider()
    {
        var http = new FakeHttpHandler().On("tinyurl.com", HttpStatusCode.OK, "https://tinyurl.com/ok");
        var outcome = await ShortenerService.CreateDefault(http.Client()).ShortenAsync("example.com", "tinyurl");

        Assert.True(outcome.IsSuccess);
        Assert.Equal("tinyurl", outcome.Final.ProviderId);
        Assert.False(outcome.UsedFallback);
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task Falls_back_when_service_is_down()
    {
        var http = new FakeHttpHandler()
            .On("is.gd", HttpStatusCode.BadGateway, "")
            .Throws("tinyurl.com", new HttpRequestException("dns"))
            .On("da.gd", HttpStatusCode.OK, "https://da.gd/ok");
        var outcome = await ShortenerService.CreateDefault(http.Client()).ShortenAsync("example.com", "is.gd");

        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.UsedFallback);
        Assert.Equal("da.gd", outcome.Final.ProviderId);
        Assert.Equal(["is.gd", "tinyurl.com", "da.gd"], http.Requests.Select(r => r.Host));
    }

    [Fact]
    public async Task No_fallback_when_disabled()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.BadGateway, "");
        var outcome = await ShortenerService.CreateDefault(http.Client()).ShortenAsync("example.com", "is.gd", allowFallback: false);

        Assert.False(outcome.IsSuccess);
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task No_fallback_for_user_errors()
    {
        var http = new FakeHttpHandler()
            .On("is.gd", HttpStatusCode.OK, """{ "errorcode": 2, "errormessage": "taken" }""")
            .On("tinyurl.com", HttpStatusCode.OK, "https://tinyurl.com/should_not_be_used");
        var outcome = await ShortenerService.CreateDefault(http.Client()).ShortenAsync("example.com", "is.gd", "my_alias");

        Assert.Equal(FailureKind.AliasRejected, outcome.Final.Failure);
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task Fallback_skips_services_that_cannot_take_the_alias()
    {
        // "long_alias_x" is valid for is.gd and TinyURL, too long for da.gd.
        var http = new FakeHttpHandler()
            .On("is.gd", HttpStatusCode.ServiceUnavailable, "")
            .On("tinyurl.com", HttpStatusCode.ServiceUnavailable, "")
            .On("v.gd", HttpStatusCode.OK, """{ "shorturl": "https://v.gd/long_alias_x" }""");
        var outcome = await ShortenerService.CreateDefault(http.Client()).ShortenAsync("example.com", "is.gd", "long_alias_x");

        Assert.True(outcome.IsSuccess);
        Assert.DoesNotContain(http.Requests, r => r.Host == "da.gd");
    }

    [Fact]
    public async Task Invalid_input_makes_no_requests()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, IsGdOk);
        var outcome = await ShortenerService.CreateDefault(http.Client()).ShortenAsync("not a url");

        Assert.Equal(FailureKind.InvalidUrl, outcome.Final.Failure);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Invalid_alias_for_preferred_provider_makes_no_requests()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, IsGdOk);
        var outcome = await ShortenerService.CreateDefault(http.Client()).ShortenAsync("example.com", "is.gd", "ab");

        Assert.Equal(FailureKind.AliasRejected, outcome.Final.Failure);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Unknown_provider_id_falls_back_to_first()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, IsGdOk);
        var outcome = await ShortenerService.CreateDefault(http.Client()).ShortenAsync("example.com", "cleanuri");

        Assert.Equal("is.gd", outcome.Final.ProviderId);
    }
}
