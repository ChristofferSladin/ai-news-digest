using System.Text;
using Digest.Ingest.Model;
using Digest.Ingest.Sources;
using Xunit;

namespace Digest.Ingest.Tests;

public sealed class FeedParserTests
{
    private const string Rss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel>
          <title>Feed</title>
          <item>
            <title>First Post</title>
            <link>https://example.com/first</link>
            <description>&lt;p&gt;Hello &lt;b&gt;world&lt;/b&gt;&lt;/p&gt;</description>
            <pubDate>Mon, 01 Jun 2026 04:00:00 GMT</pubDate>
          </item>
        </channel></rss>
        """;

    private const string Atom = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
          <title>Atom Feed</title>
          <entry>
            <title>Atom Entry</title>
            <link rel="alternate" href="https://example.com/atom-entry"/>
            <id>https://example.com/atom-entry</id>
            <summary>Some &amp; summary</summary>
            <updated>2026-06-01T04:00:00Z</updated>
          </entry>
        </feed>
        """;

    private static Stream ToStream(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public void Parses_rss_item_with_stripped_description_and_date()
    {
        IReadOnlyList<NewsItem> items = FeedParser.Parse(ToStream(Rss), "RSS", Category.AiEngineering);

        NewsItem item = Assert.Single(items);
        Assert.Equal("First Post", item.Title);
        Assert.Equal("https://example.com/first", item.Url);
        Assert.Equal("Hello world", item.Description);
        Assert.Equal(Category.AiEngineering, item.CategoryHint);
        Assert.Equal(2026, item.PublishedAt!.Value.Year);
    }

    [Fact]
    public void Parses_atom_entry_using_alternate_link()
    {
        IReadOnlyList<NewsItem> items = FeedParser.Parse(ToStream(Atom), "Atom", Category.Research);

        NewsItem item = Assert.Single(items);
        Assert.Equal("Atom Entry", item.Title);
        Assert.Equal("https://example.com/atom-entry", item.Url);
        Assert.Equal("Some & summary", item.Description);
        Assert.Equal(Category.Research, item.CategoryHint);
    }

    [Fact]
    public void Skips_entries_without_a_usable_link()
    {
        const string noLink = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel><title>F</title>
              <item><title>No link here</title></item>
            </channel></rss>
            """;

        Assert.Empty(FeedParser.Parse(ToStream(noLink), "RSS", null));
    }
}
