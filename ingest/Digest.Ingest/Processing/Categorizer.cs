using Digest.Ingest.Model;

namespace Digest.Ingest.Processing;

/// <summary>
/// Buckets an item into one of the digest categories. An agent-comms match wins outright —
/// even over source-defined buckets — so cross-cutting agent-systems work surfaces as such.
/// Otherwise source-defined buckets (arXiv → Research, r/LocalLLaMA → Local LLMs) are pinned,
/// and everything else is decided by keyword rules in priority order, falling back to the hint.
/// </summary>
public sealed class Categorizer(InterestProfile interests)
{
    private static readonly Category[] Priority =
    [
        Category.LocalLlm,
        Category.DotNetAzure,
        Category.Domain,
        Category.Research,
        Category.AiEngineering,
    ];

    public Category Categorize(NewsItem item)
    {
        string text = item.SearchText;

        // Agent-comms wins over source pins: a multi-agent / agent-protocol item is
        // agent systems even when the source would otherwise pin it (e.g. arXiv → Research).
        if (interests.CategoryKeywords.TryGetValue(Category.AgentSystems, out IReadOnlyList<Keyword>? agentKeywords) &&
            agentKeywords.Any(k => k.Matches(text)))
        {
            return Category.AgentSystems;
        }

        // Pin buckets that are defined by the source itself.
        if (item.CategoryHint is Category.Research)
        {
            return Category.Research;
        }

        if (item.CategoryHint is Category.LocalLlm)
        {
            return Category.LocalLlm;
        }

        foreach (Category category in Priority)
        {
            if (interests.CategoryKeywords.TryGetValue(category, out IReadOnlyList<Keyword>? keywords) &&
                keywords.Any(k => k.Matches(text)))
            {
                return category;
            }
        }

        return item.CategoryHint ?? Category.AiEngineering;
    }
}
