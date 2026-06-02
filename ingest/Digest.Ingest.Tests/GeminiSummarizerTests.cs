using Digest.Ingest.Configuration;
using Digest.Ingest.Model;
using Digest.Ingest.Processing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Digest.Ingest.Tests;

public sealed class GeminiSummarizerTests
{
    private static NewsItem Item(string description) =>
        new() { Title = "Some headline", Url = "https://example.com/x", SourceName = "Test", Description = description };

    private static GeminiSummarizer Create(StubChatClient client) =>
        new(client, Options.Create(new GeminiOptions { DelayBetweenCallsMs = 0 }), NullLogger<GeminiSummarizer>.Instance);

    [Fact]
    public async Task Returns_cleaned_model_text_on_success()
    {
        var client = new StubChatClient(_ =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "  A concise   summary.  "))));

        string result = await Create(client).SummarizeAsync(Item("desc"), TestContext.Current.CancellationToken);

        Assert.Equal("A concise summary.", result);
    }

    [Fact]
    public async Task Falls_back_to_description_when_model_throws()
    {
        var client = new StubChatClient(_ => Task.FromException<ChatResponse>(new InvalidOperationException("boom")));

        string result = await Create(client).SummarizeAsync(Item("Original description here"), TestContext.Current.CancellationToken);

        Assert.Equal("Original description here", result);
    }

    [Fact]
    public async Task Falls_back_to_title_when_no_description_and_model_throws()
    {
        var client = new StubChatClient(_ => Task.FromException<ChatResponse>(new HttpRequestException("503")));

        string result = await Create(client).SummarizeAsync(Item(string.Empty), TestContext.Current.CancellationToken);

        Assert.Equal("Some headline", result);
    }

    [Fact]
    public async Task Falls_back_when_model_returns_empty_text()
    {
        var client = new StubChatClient(_ =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "   "))));

        string result = await Create(client).SummarizeAsync(Item("desc fallback"), TestContext.Current.CancellationToken);

        Assert.Equal("desc fallback", result);
    }
}
