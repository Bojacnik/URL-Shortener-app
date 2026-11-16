namespace UrlShortener.Core;

/// <summary>Recognises links that are already shortened, so the app can offer to expand them instead.</summary>
public static class ShortLinks
{
    private static readonly HashSet<string> KnownHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "is.gd", "v.gd", "da.gd", "tinyurl.com", "bit.ly", "bitly.com", "j.mp", "t.co", "goo.gl",
        "ow.ly", "buff.ly", "rebrand.ly", "cutt.ly", "shorturl.at", "rb.gy", "tiny.cc", "t.ly",
        "s.id", "lnkd.in", "amzn.to", "amzn.eu", "youtu.be", "fb.me", "ift.tt", "dlvr.it",
        "shorte.st", "bl.ink", "short.io", "tiny.one", "qr.ae", "trib.al", "wp.me", "redd.it",
        "spoti.fi", "apple.co", "msft.it", "aka.ms", "g.co", "forms.gle", "maps.app.goo.gl",
    };

    /// <summary>True when the link is on a well-known shortener domain and has a short code after it.</summary>
    public static bool IsKnownShortLink(Uri url) =>
        KnownHosts.Contains(url.Host)
        && url.AbsolutePath.Trim('/').Length is > 0 and <= 40;

    public static bool IsKnownShortLink(string? text) =>
        UrlValidator.TryNormalize(text, out var url, out UrlProblem _) && IsKnownShortLink(url);
}
