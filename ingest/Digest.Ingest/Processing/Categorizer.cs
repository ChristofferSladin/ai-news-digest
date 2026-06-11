using Digest.Ingest.Model;

namespace Digest.Ingest.Processing;

/// <summary>
/// Buckets an item into one of the five digest categories. Source-defined buckets
/// (arXiv → Research, r/LocalLLaMA → Local LLMs) are pinned; everything else is decided
/// by keyword rules in priority order, falling back to the source hint.
/// </summary>
public sealed class Categorizer(InterestProfile interests)
{
    private static readonly Category[] Priority =
    [
        Category.AgentSystems,
        Category.LocalLlm,
        Category.DotNetAzure,
        Category.Domain,
        Category.Research,
        Category.AiEngineering,
    ];

    public Category Categorize(NewsItem item)
    {
        // Pin buckets that are defined by the source itself.
        if (item.CategoryHint is Category.Research)
        {
            return Category.Research;
        }

        if (item.CategoryHint is Category.LocalLlm)
        {
            return Category.LocalLlm;
        }

        string text = item.SearchText;
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
