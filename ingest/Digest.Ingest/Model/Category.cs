namespace Digest.Ingest.Model;

/// <summary>The five digest groups items are bucketed into for display.</summary>
public enum Category
{
    DotNetAzure,
    AiEngineering,
    Research,
    Domain,
    LocalLlm,
}

/// <summary>Stable slugs and human labels for <see cref="Category"/>, shared with the read API and frontend.</summary>
public static class Categories
{
    public static string ToSlug(this Category category) => category switch
    {
        Category.DotNetAzure => "dotnet-azure",
        Category.AiEngineering => "ai-engineering",
        Category.Research => "research",
        Category.Domain => "domain",
        Category.LocalLlm => "local-llms",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown category."),
    };

    public static string ToLabel(this Category category) => category switch
    {
        Category.DotNetAzure => ".NET / Azure",
        Category.AiEngineering => "AI engineering",
        Category.Research => "Research",
        Category.Domain => "Domain / accounting",
        Category.LocalLlm => "Local LLMs",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown category."),
    };
}
