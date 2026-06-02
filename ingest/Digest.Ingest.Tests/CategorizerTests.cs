using Digest.Ingest.Model;
using Digest.Ingest.Processing;
using Xunit;

namespace Digest.Ingest.Tests;

public sealed class CategorizerTests
{
    private readonly Categorizer _categorizer = new(new InterestProfile());

    private static NewsItem Item(string title, Category? hint = null) =>
        new() { Title = title, Url = "https://example.com/x", SourceName = "Test", CategoryHint = hint };

    [Fact]
    public void Research_hint_is_pinned_regardless_of_text()
    {
        Assert.Equal(Category.Research, _categorizer.Categorize(Item("Some agent paper", Category.Research)));
    }

    [Fact]
    public void LocalLlm_hint_is_pinned()
    {
        Assert.Equal(Category.LocalLlm, _categorizer.Categorize(Item("Weekly discussion", Category.LocalLlm)));
    }

    [Fact]
    public void DotNet_content_buckets_to_dotnet_azure()
    {
        Assert.Equal(Category.DotNetAzure, _categorizer.Categorize(Item("What's new in C# 14")));
    }

    [Fact]
    public void Local_llm_keywords_override_a_generic_hint()
    {
        Assert.Equal(Category.LocalLlm, _categorizer.Categorize(Item("Running Ollama locally", Category.AiEngineering)));
    }

    [Fact]
    public void Domain_keywords_bucket_to_domain()
    {
        Assert.Equal(Category.Domain, _categorizer.Categorize(Item("Using AI for accounting and invoice processing")));
    }

    [Fact]
    public void Falls_back_to_ai_engineering_by_default()
    {
        Assert.Equal(Category.AiEngineering, _categorizer.Categorize(Item("Prompt engineering tips for retrieval")));
    }

    [Theory]
    [InlineData(Category.DotNetAzure, "dotnet-azure", ".NET / Azure")]
    [InlineData(Category.LocalLlm, "local-llms", "Local LLMs")]
    [InlineData(Category.Domain, "domain", "Domain / accounting")]
    public void Category_slug_and_label_are_stable(Category category, string slug, string label)
    {
        Assert.Equal(slug, category.ToSlug());
        Assert.Equal(label, category.ToLabel());
    }
}
