using Digest.Ingest.Configuration;
using Digest.Ingest.Model;
using Digest.Ingest.Processing;
using Microsoft.Extensions.Options;
using Xunit;

namespace Digest.Ingest.Tests;

/// <summary>
/// Integration gate: proves that categorisation (T2) and selection (T1) <em>compose</em> into the
/// right ~15. Drives the real pipeline ordering used by <c>IngestRunner</c> — every item is run
/// through <see cref="Categorizer.Categorize"/> to set its <see cref="NewsItem.Category"/>, scored,
/// then the whole set is run through <see cref="DigestSelector.Select"/> — and asserts the digest
/// invariants hold on the final selected set.
/// </summary>
public sealed class CompositionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 5, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A raw item with the real descriptive fields a source would set. Its <see cref="NewsItem.Score"/>
    /// is assigned later from a deterministic per-title table (see <see cref="ScoreFor"/>) so the
    /// composition is reproducible and independent of scorer-weight tuning, while the categorisation
    /// half of the flow runs for real. Description defaults to empty so <see cref="NewsItem.SearchText"/>
    /// equals the title and each item lands only in the bucket its title implies.
    /// </summary>
    private static NewsItem Raw(string title, Category? hint = null, string description = "") =>
        new()
        {
            Title = title,
            Description = description,
            Url = $"https://example.com/{Guid.NewGuid():N}",
            SourceName = "Test",
            CategoryHint = hint,
            PublishedAt = Now,
        };

    [Fact]
    public void Categorise_then_select_yields_a_balanced_digest_of_at_most_max_items()
    {
        var options = new IngestOptions(); // defaults: Max 15, LocalLlmCap 3, ResearchFloor 3, AgentSystemsFloor 1, MinScore 1.0
        var categorizer = new Categorizer(new InterestProfile());
        var selector = new DigestSelector(Options.Create(options));

        // The agent-comms arXiv paper: the source hints Research, but the title is pure agent-systems
        // ("agent-to-agent", "long-horizon"), so T2 must re-bucket it to AgentSystems — and its low
        // score means only the T1 AgentSystems floor can keep it in the final cut.
        NewsItem multiAgentArxiv = Raw("Agent-to-agent orchestration for long-horizon tasks", hint: Category.Research);

        var raw = new List<NewsItem>
        {
            multiAgentArxiv,

            // Five Local-LLM items, all high-scoring — without the cap they would flood the digest;
            // the LocalLlmCap (3) must hold them to three.
            Raw("Ollama adds first-class GGUF import"),
            Raw("vLLM throughput on consumer GPUs"),
            Raw("LM Studio ships a faster runtime"),
            Raw("llama.cpp lands a big speedup"),
            Raw("Quantized open-weight models compared"),

            // Four Research items (source-pinned). Two are barely above MinScore, so they would be
            // crowded out by the high-scoring filler without the ResearchFloor (3).
            Raw("A new dataset for retrieval benchmarks", hint: Category.Research),
            Raw("Pretraining scaling laws revisited", hint: Category.Research),
            Raw("Transformer survey of architectures", hint: Category.Research),
            Raw("State-of-the-art image classification", hint: Category.Research),

            // High-scoring filler across the remaining buckets, enough to push the raw set past MaxItems
            // and to contend for every non-floor slot. None carry agent-comms or local-LLM keywords.
            Raw("What's new in C# 14 and .NET 10"),
            Raw("Blazor render-mode deep dive"),
            Raw("Azure OpenAI adds a new region"),
            Raw("Bookkeeping automation for SMBs"),
            Raw("Invoice reconciliation with ERP systems"),
            Raw("Fraud detection in fintech ledgers"),
            Raw("Prompt engineering patterns for RAG"),
            Raw("Embeddings and vector search tips"),
            Raw("Evals for retrieval pipelines"),
            Raw("Production inference latency tuning"),
        };

        // --- Drive the real flow, exactly as IngestRunner does: categorise, score, then select. ---
        foreach (NewsItem item in raw)
        {
            item.Category = categorizer.Categorize(item);
            // Score is assigned per-title from a deterministic table keyed on the URL-free title.
            item.Score = ScoreFor(item.Title);
        }

        IReadOnlyList<NewsItem> selected = selector.Select(raw);

        // --- Composition invariants on the FINAL selected set. ---
        Assert.True(selected.Count <= options.MaxItems,
            $"expected ≤ {options.MaxItems} items, got {selected.Count}");

        Assert.True(selected.Count(i => i.Category == Category.LocalLlm) <= options.LocalLlmCap,
            $"expected Local-LLM count ≤ {options.LocalLlmCap} (cap), got {selected.Count(i => i.Category == Category.LocalLlm)}");

        Assert.True(selected.Count(i => i.Category == Category.Research) >= options.ResearchFloor,
            $"expected Research count ≥ {options.ResearchFloor} (floor), got {selected.Count(i => i.Category == Category.Research)}");

        Assert.True(selected.Count(i => i.Category == Category.AgentSystems) >= options.AgentSystemsFloor,
            $"expected Agent-systems count ≥ {options.AgentSystemsFloor} (floor), got {selected.Count(i => i.Category == Category.AgentSystems)}");

        // The star of the integration: the agent-comms arXiv paper survived T2 re-bucketing AND the
        // T1 floor guarantee, despite a near-lowest score.
        Assert.Contains(multiAgentArxiv, selected);
        Assert.Equal(Category.AgentSystems, multiAgentArxiv.Category);

        // Map the table back onto each kept item's title to assert the scores actually drove selection.
        Assert.All(selected, i => Assert.True(i.Score >= options.MinScore,
            $"selected item below MinScore slipped through: {i.Title} ({i.Score})"));
    }

    /// <summary>Deterministic score table keyed on title — mirrors what a scorer would emit, without its tuning.</summary>
    private static double ScoreFor(string title) => title switch
    {
        "Agent-to-agent orchestration for long-horizon tasks" => 1.1,
        "Ollama adds first-class GGUF import" => 13,
        "vLLM throughput on consumer GPUs" => 12,
        "LM Studio ships a faster runtime" => 11,
        "llama.cpp lands a big speedup" => 10,
        "Quantized open-weight models compared" => 9,
        "A new dataset for retrieval benchmarks" => 5,
        "Pretraining scaling laws revisited" => 4,
        "Transformer survey of architectures" => 1.3,
        "State-of-the-art image classification" => 1.2,
        "What's new in C# 14 and .NET 10" => 8,
        "Blazor render-mode deep dive" => 7,
        "Azure OpenAI adds a new region" => 6.5,
        "Bookkeeping automation for SMBs" => 6,
        "Invoice reconciliation with ERP systems" => 5.5,
        "Fraud detection in fintech ledgers" => 5,
        "Prompt engineering patterns for RAG" => 4.5,
        "Embeddings and vector search tips" => 4,
        "Evals for retrieval pipelines" => 3.5,
        "Production inference latency tuning" => 3,
        _ => 0,
    };
}
