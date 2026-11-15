using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace UrlShortener.Core;

public static class UrlValidator
{
    /// <summary>
    /// Turns user input into an absolute http(s) URL. A missing scheme gets "https://".
    /// Rejects things public shorteners refuse anyway: other schemes, bare words, localhost and private IPs.
    /// </summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out Uri? url, [NotNullWhen(false)] out string? error)
    {
        url = null;
        var text = input?.Trim() ?? "";

        if (text.Length == 0)
        {
            error = "Enter a URL to shorten.";
            return false;
        }

        if (text.Any(char.IsWhiteSpace))
        {
            error = "A URL cannot contain spaces.";
            return false;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var candidate))
        {
            error = "That doesn't look like a valid URL.";
            return false;
        }

        if (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps)
        {
            error = "Only http:// and https:// links can be shortened.";
            return false;
        }

        var host = candidate.IdnHost;
        if (candidate.IsLoopback || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            error = "Local addresses can't be shortened by a public service.";
            return false;
        }

        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            if (IsPrivate(ip))
            {
                error = "Private network addresses can't be shortened by a public service.";
                return false;
            }
        }
        else if (!host.Contains('.') || host.EndsWith('.') || host.StartsWith('.'))
        {
            error = "The address needs a full domain name, like example.com.";
            return false;
        }

        url = candidate;
        error = null;
        return true;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;

        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || b[0] == 0;
    }
}
