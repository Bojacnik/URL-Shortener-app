using System.Net;
using UrlShortener.Core;
using UrlShortener.Core.Providers;

namespace UrlShortener.Tests;

public class ProviderTests
{
    private static readonly Uri LongUrl = new("https://example.com/a b?x=1&y=2");

    [Fact]
    public async Task IsGd_success_and_request_is_encoded()
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, """{ "shorturl": "https://is.gd/abc123" }""");
        var result = await new IsGdProvider(http.Client()).ShortenAsync(LongUrl, "my_link");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://is.gd/abc123", result.ShortUrl);
        var query = http.Requests.Single().Query;
        Assert.Contains("format=json", query);
        Assert.Contains("url=https%3A%2F%2Fexample.com%2Fa%2520b%3Fx%3D1%26y%3D2", query);
        Assert.Contains("shorturl=my_link", query);
    }

    [Fact]
    public async Task IsGd_nonstandard_charset_header_is_handled()
    {
        // Real is.gd responses carry "Content-Type: text/javascript; charset=utf8", which .NET won't decode by name.
        var http = new FakeHttpHandler().On("is.gd", _ =>
        {
            var content = new ByteArrayContent("""{ "shorturl": "https://is.gd/abc" }"""u8.ToArray());
            content.Headers.TryAddWithoutValidation("Content-Type", "text/javascript; charset=utf8");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var result = await new IsGdProvider(http.Client()).ShortenAsync(LongUrl, null);

        Assert.Equal("https://is.gd/abc", result.ShortUrl);
    }

    [Theory]
    [InlineData(1, FailureKind.InvalidUrl)]
    [InlineData(2, FailureKind.AliasRejected)]
    [InlineData(3, FailureKind.RateLimited)]
    [InlineData(4, FailureKind.ServiceError)]
    public async Task IsGd_maps_error_codes(int code, FailureKind expected)
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, $$"""{ "errorcode": {{code}}, "errormessage": "nope" }""");
        var result = await new IsGdProvider(http.Client()).ShortenAsync(LongUrl, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Failure);
    }

    [Theory]
    [InlineData("<html>maintenance</html>")]
    [InlineData("""{ "shorturl": "https://evil.example/phish" }""")]
    [InlineData("""{ "shorturl": "javascript:alert(1)" }""")]
    public async Task IsGd_rejects_garbage_or_foreign_links(string body)
    {
        var http = new FakeHttpHandler().On("is.gd", HttpStatusCode.OK, body);
        var result = await new IsGdProvider(http.Client()).ShortenAsync(LongUrl, null);

        Assert.Equal(FailureKind.ServiceError, result.Failure);
    }

    [Fact]
    public async Task VGd_uses_its_own_host()
    {
        var http = new FakeHttpHandler().On("v.gd", HttpStatusCode.OK, """{ "shorturl": "https://v.gd/xyz" }""");
        var result = await new IsGdProvider(http.Client(), "v.gd").ShortenAsync(LongUrl, null);

        Assert.Equal("https://v.gd/xyz", result.ShortUrl);
        Assert.Equal("v.gd", result.ProviderId);
    }

    [Fact]
    public async Task TinyUrl_success_and_taken_alias()
    {
        var ok = new FakeHttpHandler().On("tinyurl.com", HttpStatusCode.OK, "https://tinyurl.com/2abc\n");
        Assert.Equal("https://tinyurl.com/2abc", (await new TinyUrlProvider(ok.Client()).ShortenAsync(LongUrl, null)).ShortUrl);

        var taken = new FakeHttpHandler().On("tinyurl.com", HttpStatusCode.BadRequest, "Error");
        var result = await new TinyUrlProvider(taken.Client()).ShortenAsync(LongUrl, "taken_alias");
        Assert.Equal(FailureKind.AliasRejected, result.Failure);
    }

    [Fact]
    public async Task DaGd_rejects_long_alias_without_calling_the_service()
    {
        var http = new FakeHttpHandler().On("da.gd", HttpStatusCode.OK, "https://da.gd/x");
        var result = await new DaGdProvider(http.Client()).ShortenAsync(LongUrl, "elevenchars");

        Assert.Equal(FailureKind.AliasRejected, result.Failure);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Server_errors_and_429_are_retryable()
    {
        var down = new FakeHttpHandler().On("is.gd", HttpStatusCode.ServiceUnavailable, "");
        var r1 = await new IsGdProvider(down.Client()).ShortenAsync(LongUrl, null);
        Assert.Equal(FailureKind.ServiceError, r1.Failure);
        Assert.True(r1.ShouldTryNextProvider);

        var throttled = new FakeHttpHandler().On("da.gd", HttpStatusCode.TooManyRequests, "");
        var r2 = await new DaGdProvider(throttled.Client()).ShortenAsync(LongUrl, null);
        Assert.Equal(FailureKind.RateLimited, r2.Failure);
    }

    [Fact]
    public async Task Network_failure_is_reported_not_thrown()
    {
        var http = new FakeHttpHandler().Throws("tinyurl.com", new HttpRequestException("boom"));
        var result = await new TinyUrlProvider(http.Client()).ShortenAsync(LongUrl, null);

        Assert.Equal(FailureKind.Network, result.Failure);
        Assert.True(result.ShouldTryNextProvider);
    }

    [Theory]
    [InlineData("is.gd", "abcd", false)]      // too short
    [InlineData("is.gd", "abc-def", false)]   // dash not allowed
    [InlineData("is.gd", "abc_def", true)]
    [InlineData("tinyurl", "abc-def", true)]
    [InlineData("da.gd", "x", true)]
    public void Alias_rules(string providerId, string alias, bool valid)
    {
        var service = ShortenerService.CreateDefault(new HttpClient());
        Assert.Equal(valid, service.Find(providerId)!.ValidateAlias(alias) is null);
    }
}
