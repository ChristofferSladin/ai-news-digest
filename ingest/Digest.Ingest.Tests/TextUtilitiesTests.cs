using Digest.Ingest.Text;
using Xunit;

namespace Digest.Ingest.Tests;

public sealed class TextUtilitiesTests
{
    [Fact]
    public void StripHtml_removes_tags_decodes_entities_and_collapses_whitespace()
    {
        string result = TextUtilities.StripHtml("<p>Hello&nbsp;&amp; <strong>welcome</strong>\n  world</p>");
        Assert.Equal("Hello & welcome world", result);
    }

    [Fact]
    public void StripHtml_returns_empty_for_null_or_blank()
    {
        Assert.Equal(string.Empty, TextUtilities.StripHtml(null));
        Assert.Equal(string.Empty, TextUtilities.StripHtml("   "));
    }

    [Theory]
    [InlineData("https://example.com/post?utm_source=rss&id=5#section", "https://example.com/post?id=5")]
    [InlineData("https://EXAMPLE.com/post/", "https://example.com/post")]
    [InlineData("https://example.com/post?utm_campaign=x", "https://example.com/post")]
    public void NormalizeUrl_strips_tracking_fragment_case_and_trailing_slash(string input, string expected)
    {
        Assert.Equal(expected, TextUtilities.NormalizeUrl(input));
    }

    [Fact]
    public void NormalizeUrl_makes_equivalent_urls_dedupe_equal()
    {
        string a = TextUtilities.NormalizeUrl("https://example.com/a?utm_source=hn&ref=feed");
        string b = TextUtilities.NormalizeUrl("https://example.com/a/");
        Assert.Equal(a, b);
    }

    [Fact]
    public void NormalizeUrl_returns_input_for_non_http()
    {
        Assert.Equal("not a url", TextUtilities.NormalizeUrl("not a url"));
    }

    [Theory]
    [InlineData("anthropic-acquires-stainless", "Anthropic acquires stainless")]
    [InlineData("claude_is_a_space", "Claude is a space")]
    public void SlugToTitle_humanises_slugs(string slug, string expected)
    {
        Assert.Equal(expected, TextUtilities.SlugToTitle(slug));
    }

    [Fact]
    public void Truncate_breaks_on_word_boundary_and_adds_ellipsis()
    {
        string result = TextUtilities.Truncate("the quick brown fox jumps", 12);
        Assert.True(result.Length <= 13); // 12 + ellipsis
        Assert.EndsWith("…", result, StringComparison.Ordinal);
        Assert.DoesNotContain("jumps", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncate_returns_input_when_short_enough()
    {
        Assert.Equal("short", TextUtilities.Truncate("short", 50));
    }
}
