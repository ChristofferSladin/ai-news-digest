using Digest.Ingest.Model;
using Digest.Ingest.Processing;
using Xunit;

namespace Digest.Ingest.Tests;

public sealed class RelevanceScorerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 5, 0, 0, TimeSpan.Zero);
    private readonly RelevanceScorer _scorer = new(new InterestProfile(), new FixedTimeProvider(Now));

    private static NewsItem Item(string title, string description = "", DateTimeOffset? publishedAt = null) =>
        new() { Title = title, Url = "https://example.com/x", SourceName = "Test", Description = description, PublishedAt = publishedAt };

    [Fact]
    public void Title_match_outweighs_body_match_for_the_same_keyword()
    {
        double inTitle = _scorer.Score(Item("Semantic Kernel ships v2"));
        double inBody = _scorer.Score(Item("Weekly roundup", "now featuring semantic kernel"));

        Assert.True(inTitle > inBody, $"expected title score {inTitle} > body score {inBody}");
    }

    [Fact]
    public void Recent_items_score_higher_than_older_ones()
    {
        double recent = _scorer.Score(Item("Azure OpenAI update", publishedAt: Now));
        double older = _scorer.Score(Item("Azure OpenAI update", publishedAt: Now.AddDays(-5)));

        Assert.True(recent > older, $"expected recent score {recent} > older score {older}");
    }

    [Fact]
    public void Irrelevant_item_scores_zero()
    {
        Assert.Equal(0, _scorer.Score(Item("Sourdough baking tips", "how to grow tomatoes")));
    }

    [Fact]
    public void Highly_relevant_item_clears_the_default_min_score()
    {
        double score = _scorer.Score(Item("Microsoft.Extensions.AI and RAG with Semantic Kernel", publishedAt: Now));
        Assert.True(score >= 1.0);
    }
}
