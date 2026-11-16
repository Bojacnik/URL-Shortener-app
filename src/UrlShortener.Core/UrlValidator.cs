using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;

namespace UrlShortener.Core;

/// <summary>Why a piece of text is not a usable link. The UI turns these into translated messages.</summary>
public enum UrlProblem
{
    Empty,
    Spaces,
    Invalid,
    Scheme,
    LocalAddress,
    NoDomain,
}

public static partial class UrlValidator
{
    /// <summary>
    /// Turns user input into an absolute http(s) URL. A missing scheme gets "https://".
    /// Rejects things public services refuse anyway: other schemes, bare words, localhost and private IPs.
    /// </summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out Uri? url, out UrlProblem problem)
    {
        url = null;
        problem = default;
        var text = input?.Trim() ?? "";

        if (text.Length == 0)
            return Fail(UrlProblem.Empty, out problem);

        if (text.Any(char.IsWhiteSpace))
            return Fail(UrlProblem.Spaces, out problem);

        if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var candidate))
            return Fail(UrlProblem.Invalid, out problem);

        if (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps)
            return Fail(UrlProblem.Scheme, out problem);

        if (IsLocalOrPrivate(candidate))
            return Fail(UrlProblem.LocalAddress, out problem);

        if (!IPAddress.TryParse(candidate.IdnHost.Trim('[', ']'), out _))
        {
            var host = candidate.IdnHost;
            if (!host.Contains('.') || host.EndsWith('.') || host.StartsWith('.'))
                return Fail(UrlProblem.NoDomain, out problem);
        }

        url = candidate;
        return true;
    }

    /// <summary>Same as the other overload, with an English message for logs, tools and tests.</summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out Uri? url, [NotNullWhen(false)] out string? error)
    {
        if (TryNormalize(input, out url, out UrlProblem problem))
        {
            error = null;
            return true;
        }
        error = Describe(problem);
        return false;
    }

    public static string Describe(UrlProblem problem) => problem switch
    {
        UrlProblem.Empty => "Enter a URL.",
        UrlProblem.Spaces => "A URL cannot contain spaces.",
        UrlProblem.Invalid => "That doesn't look like a valid URL.",
        UrlProblem.Scheme => "Only http:// and https:// links work.",
        UrlProblem.LocalAddress => "Local and private network addresses can't be used with a public service.",
        UrlProblem.NoDomain => "The address needs a full domain name, like example.com.",
        _ => "Invalid URL.",
    };

    /// <summary>
    /// Pulls the first link out of free text, such as an Android share ("Check this out https://…").
    /// Falls back to the whole trimmed text when there is no http(s) link in it.
    /// </summary>
    public static string ExtractUrl(string? text)
    {
        var trimmed = text?.Trim() ?? "";
        var match = LinkInText().Match(trimmed);
        return match.Success ? match.Value.TrimEnd('.', ',', ';', ')', ']', '!', '?') : trimmed;
    }

    /// <summary>True for localhost, loopback and private/link-local IP addresses.</summary>
    public static bool IsLocalOrPrivate(Uri url)
    {
        var host = url.IdnHost;
        if (url.IsLoopback || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return true;
        return IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IsPrivate(ip);
    }

    private static bool Fail(UrlProblem value, out UrlProblem problem)
    {
        problem = value;
        return false;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;

        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] == 0;
    }

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex LinkInText();
}
