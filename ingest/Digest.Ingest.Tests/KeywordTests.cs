using Digest.Ingest.Processing;
using Xunit;

namespace Digest.Ingest.Tests;

public sealed class KeywordTests
{
    [Theory]
    [InlineData("ai", "the new ai model", true)]
    [InlineData("ai", "training a network", false)]   // must not match inside "training"
    [InlineData("ai", "available now", false)]        // must not match inside "available"
    [InlineData("rag", "a rag pipeline", true)]
    [InlineData("rag", "a fragment of code", false)]  // must not match inside "fragment"
    [InlineData("llm", "local llm runtime", true)]
    public void Alphanumeric_keywords_match_on_word_boundaries(string term, string text, bool expected)
    {
        Assert.Equal(expected, new Keyword(term).Matches(text));
    }

    [Theory]
    [InlineData(".net", "shipping asp.net core 10", true)]      // substring; punctuation-led term
    [InlineData(".net", "the internet", false)]
    [InlineData("c#", "written in c# today", true)]
    [InlineData("llama.cpp", "built on llama.cpp", true)]
    [InlineData("microsoft.extensions.ai", "uses microsoft.extensions.ai abstractions", true)]
    public void Punctuation_keywords_match_as_substrings(string term, string text, bool expected)
    {
        Assert.Equal(expected, new Keyword(term).Matches(text));
    }

    [Fact]
    public void Matching_is_case_insensitive_for_lowercased_input()
    {
        // Matcher assumes lower-cased input, mirroring how callers prepare text.
        Assert.True(new Keyword("Semantic Kernel").Matches("using semantic kernel here"));
    }
}
