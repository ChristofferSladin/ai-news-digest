using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Digest.Ingest.Text;

/// <summary>
/// Small, dependency-free text helpers shared across sources and processing:
/// HTML-to-text, length-bounded truncation, URL canonicalisation for dedupe,
/// and slug-to-title conversion for scraped links.
/// </summary>
internal static partial class TextUtilities
{
    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "utm_id",
        "ref", "ref_src", "ref_source", "source", "fbclid", "gclid", "mc_cid", "mc_eid",
    };

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    /// <summary>Converts an HTML or text fragment into a single line of clean, decoded text.</summary>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        string withoutTags = HtmlTagPattern().Replace(html, " ");
        string decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespacePattern().Replace(decoded, " ").Trim();
    }

    /// <summary>Truncates to at most <paramref name="maxLength"/> characters on a word boundary, adding an ellipsis.</summary>
    public static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        int cut = text.LastIndexOf(' ', Math.Min(maxLength, text.Length - 1));
        if (cut <= 0)
        {
            cut = maxLength;
        }

        return string.Concat(text.AsSpan(0, cut).TrimEnd(), "…");
    }

    /// <summary>
    /// Canonicalises a URL so the same article from different surfaces dedupes to one key:
    /// lower-cased scheme/host, no fragment, tracking query parameters removed, no trailing slash.
    /// Returns the trimmed input unchanged when it cannot be parsed as an absolute URL.
    /// </summary>
    public static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        string trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return trimmed;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty,
        };

        if (!string.IsNullOrEmpty(uri.Query))
        {
            IEnumerable<string> kept = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(pair => !TrackingParameters.Contains(pair.Split('=', 2)[0]));
            builder.Query = string.Join('&', kept);
        }

        // Drop the default port and any trailing slash on the path for a stable key.
        if ((builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80) ||
            (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443))
        {
            builder.Port = -1;
        }

        string path = builder.Path.Length > 1 ? builder.Path.TrimEnd('/') : builder.Path;
        builder.Path = path;

        return builder.Uri.AbsoluteUri;
    }

    /// <summary>Turns a URL slug such as "anthropic-acquires-stainless" into "Anthropic acquires stainless".</summary>
    public static string SlugToTitle(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return string.Empty;
        }

        string spaced = slug.Replace('-', ' ').Replace('_', ' ').Trim();
        spaced = WhitespacePattern().Replace(spaced, " ");
        if (spaced.Length == 0)
        {
            return string.Empty;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{char.ToUpperInvariant(spaced[0])}{spaced[1..]}");
    }

    /// <summary>Collapses whitespace and trims; returns empty string for null/blank input.</summary>
    public static string Clean(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : WhitespacePattern().Replace(text, " ").Trim();
}
