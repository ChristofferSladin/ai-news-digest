using System.ServiceModel.Syndication;
using System.Xml;
using Digest.Ingest.Model;
using Digest.Ingest.Text;

namespace Digest.Ingest.Sources;

/// <summary>Parses an RSS 2.0 or Atom 1.0 stream into <see cref="NewsItem"/>s.</summary>
internal static class FeedParser
{
    public static IReadOnlyList<NewsItem> Parse(Stream xml, string sourceName, Category? categoryHint)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null, // never resolve external entities
            MaxCharactersFromEntities = 1024 * 1024,
            CloseInput = false,
        };

        using XmlReader reader = XmlReader.Create(xml, settings);
        SyndicationFeed feed = SyndicationFeed.Load(reader);

        var items = new List<NewsItem>(capacity: 64);
        foreach (SyndicationItem entry in feed.Items)
        {
            string title = TextUtilities.Clean(entry.Title?.Text);
            string? url = ExtractLink(entry);
            if (title.Length == 0 || string.IsNullOrEmpty(url))
            {
                continue;
            }

            items.Add(new NewsItem
            {
                Title = title,
                Url = url,
                SourceName = sourceName,
                Description = TextUtilities.StripHtml(ExtractContent(entry)),
                PublishedAt = ExtractTimestamp(entry),
                CategoryHint = categoryHint,
            });
        }

        return items;
    }

    private static string? ExtractLink(SyndicationItem entry)
    {
        // Prefer an explicit alternate link, then any link, then the Atom id when it is a URL.
        SyndicationLink? alternate = entry.Links.FirstOrDefault(l =>
            string.IsNullOrEmpty(l.RelationshipType) ||
            string.Equals(l.RelationshipType, "alternate", StringComparison.OrdinalIgnoreCase));

        Uri? uri = alternate?.Uri ?? entry.Links.FirstOrDefault()?.Uri;
        if (uri is not null)
        {
            return uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
        }

        if (!string.IsNullOrEmpty(entry.Id) &&
            Uri.TryCreate(entry.Id, UriKind.Absolute, out Uri? idUri) &&
            (idUri.Scheme == Uri.UriSchemeHttp || idUri.Scheme == Uri.UriSchemeHttps))
        {
            return idUri.AbsoluteUri;
        }

        return null;
    }

    private static string? ExtractContent(SyndicationItem entry)
    {
        if (entry.Summary?.Text is { Length: > 0 } summary)
        {
            return summary;
        }

        return entry.Content as TextSyndicationContent is { Text.Length: > 0 } content
            ? content.Text
            : null;
    }

    private static DateTimeOffset? ExtractTimestamp(SyndicationItem entry)
    {
        if (entry.PublishDate > DateTimeOffset.MinValue)
        {
            return entry.PublishDate;
        }

        return entry.LastUpdatedTime > DateTimeOffset.MinValue ? entry.LastUpdatedTime : null;
    }
}
