using Digest.Ingest.Configuration;
using Digest.Ingest.Model;
using Digest.Ingest.Processing;
using Microsoft.Extensions.Options;
using Xunit;

namespace Digest.Ingest.Tests;

public sealed class DigestSelectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 5, 0, 0, TimeSpan.Zero);

    private static DigestSelector Selector(IngestOptions? options = null) =>
        new(Options.Create(options ?? new IngestOptions()));

    private static NewsItem Item(Category category, double score, DateTimeOffset? publishedAt = null) =>
        new()
        {
            Title = $"{category} {score}",
            Url = $"https://example.com/{category}-{score}-{Guid.NewGuid():N}",
            SourceName = "Test",
            Category = category,
            Score = score,
            PublishedAt = publishedAt ?? Now,
        };

    [Fact]
    public void LocalLlm_is_capped_at_the_configured_cap()
    {
        var scored = new List<NewsItem>
        {
            Item(Category.LocalLlm, 10),
            Item(Category.LocalLlm, 9),
            Item(Category.LocalLlm, 8),
            Item(Category.LocalLlm, 7),
            Item(Category.LocalLlm, 6),
            Item(Category.AiEngineering, 5),
            Item(Category.AiEngineering, 4),
            Item(Category.DotNetAzure, 3),
        };

        IReadOnlyList<NewsItem> result = Selector().Select(scored);

        Assert.Equal(3, result.Count(i => i.Category == Category.LocalLlm));
    }

    [Fact]
    public void Research_floor_is_pulled_in_over_higher_scoring_items()
    {
        var scored = new List<NewsItem>
        {
            // Three low-scoring Research items, all above MinScore but below everything else.
            Item(Category.Research, 1.5),
            Item(Category.Research, 1.4),
            Item(Category.Research, 1.3),
            // Plenty of higher-scoring items that would otherwise crowd Research out.
            Item(Category.LocalLlm, 10),
            Item(Category.LocalLlm, 9),
            Item(Category.LocalLlm, 8),
            Item(Category.AiEngineering, 7),
            Item(Category.AiEngineering, 6),
            Item(Category.DotNetAzure, 5),
            Item(Category.DotNetAzure, 4),
        };

        IReadOnlyList<NewsItem> result = Selector().Select(scored);

        Assert.True(
            result.Count(i => i.Category == Category.Research) >= 3,
            "expected the Research floor (3) to be guaranteed over higher-scoring items");
    }

    [Fact]
    public void A_single_low_scoring_agent_systems_item_is_guaranteed()
    {
        NewsItem agent = Item(Category.AgentSystems, 1.1);
        var scored = new List<NewsItem>
        {
            agent,
            Item(Category.LocalLlm, 10),
            Item(Category.LocalLlm, 9),
            Item(Category.AiEngineering, 8),
            Item(Category.AiEngineering, 7),
            Item(Category.DotNetAzure, 6),
            Item(Category.DotNetAzure, 5),
        };

        IReadOnlyList<NewsItem> result = Selector().Select(scored);

        Assert.Contains(agent, result);
    }

    [Fact]
    public void A_floor_is_never_padded_with_an_item_below_min_score()
    {
        // The only AgentSystems item scores below MinScore (default 1.0); the floor must NOT force it in.
        NewsItem belowFloor = Item(Category.AgentSystems, 0.5);
        var scored = new List<NewsItem>
        {
            belowFloor,
            Item(Category.AiEngineering, 8),
            Item(Category.DotNetAzure, 6),
        };

        IReadOnlyList<NewsItem> result = Selector().Select(scored);

        Assert.DoesNotContain(belowFloor, result);
    }

    [Fact]
    public void Total_never_exceeds_max_items()
    {
        var options = new IngestOptions { MaxItems = 15 };
        List<NewsItem> scored = Enumerable.Range(0, 40)
            .Select(n => Item(Category.AiEngineering, 5 + n * 0.1))
            .ToList();

        IReadOnlyList<NewsItem> result = Selector(options).Select(scored);

        Assert.True(result.Count <= options.MaxItems, $"expected ≤ {options.MaxItems}, got {result.Count}");
    }

    [Fact]
    public void Equal_scores_are_ordered_by_recency_descending()
    {
        NewsItem older = Item(Category.AiEngineering, 5, publishedAt: Now.AddDays(-2));
        NewsItem newer = Item(Category.AiEngineering, 5, publishedAt: Now);

        // Supply oldest-first so a stable sort would leave them mis-ordered without the tie-break.
        IReadOnlyList<NewsItem> result = Selector().Select([older, newer]);

        Assert.Equal([newer, older], result);
    }
}
