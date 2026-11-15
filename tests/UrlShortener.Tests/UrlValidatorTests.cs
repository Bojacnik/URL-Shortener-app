using UrlShortener.Core;

namespace UrlShortener.Tests;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("  http://example.com/path?q=1#frag  ", "http://example.com/path?q=1#frag")]
    [InlineData("example.com/page", "https://example.com/page")]
    [InlineData("www.youtube.com/watch?v=abc", "https://www.youtube.com/watch?v=abc")]
    [InlineData("https://8.8.8.8/dns", "https://8.8.8.8/dns")]
    [InlineData("https://příklad.cz", "https://příklad.cz/")]
    public void Accepts_and_normalizes(string input, string expected)
    {
        Assert.True(UrlValidator.TryNormalize(input, out var url, out var error), error);
        Assert.Equal(expected, url.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("hello world.com")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://10.0.0.5/x")]
    [InlineData("https://example.")]
    public void Rejects(string input)
    {
        Assert.False(UrlValidator.TryNormalize(input, out var url, out var error));
        Assert.Null(url);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
