using System.ClientModel;
using System.ClientModel.Primitives;
using Digest.Ingest.Configuration;
using Digest.Ingest.Model;
using Digest.Ingest.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Digest.Ingest.Processing;

/// <summary>
/// Summarises items through a provider-swappable <see cref="IChatClient"/>. Transient
/// failures never abort the run: on error or empty output it falls back to a trimmed
/// source description (or the title).
/// </summary>
internal sealed class GeminiSummarizer(
    IChatClient chatClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiSummarizer> logger) : ISummarizer
{
    private const string SystemPrompt =
        "You are a precise technology-news editor for a personal AI and .NET engineering digest. " +
        "Summarise the item in ONE or at most TWO sentences. Be factual, concrete and neutral: no hype, " +
        "no opinions, no marketing language. Never invent details, numbers, quotes or claims that are not " +
        "present in the input. If only a title is provided, describe what the item is about based solely on " +
        "the title and source. Respond with the summary text only — no preamble, labels or quotation marks.";

    public async Task<string> SummarizeAsync(NewsItem item, CancellationToken cancellationToken)
    {
        GeminiOptions opt = options.Value;

        List<ChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, BuildUserPrompt(item)),
        ];

        var chatOptions = new ChatOptions
        {
            Temperature = opt.Temperature,
            MaxOutputTokens = opt.MaxOutputTokens,
        };

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                ChatResponse response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                string text = TextUtilities.Clean(response.Text);
                return text.Length > 0 ? text : Fallback(item);
            }
            // Rate limited (free-tier RPM): wait out the window and retry instead of giving up.
            catch (ClientResultException ex)
                when (IsRateLimited(ex) && attempt <= opt.MaxRateLimitRetries && !cancellationToken.IsCancellationRequested)
            {
                TimeSpan wait = RetryAfter(ex) ?? TimeSpan.FromMilliseconds(opt.RateLimitRetryDelayMs);
                logger.LogWarning("Rate limited summarising {Url}; waiting {Seconds:n0}s then retry {Attempt}/{Max}",
                    item.Url, wait.TotalSeconds, attempt, opt.MaxRateLimitRetries);
                await Task.Delay(wait, cancellationToken);
            }
            // Anything else (incl. HTTP timeouts) → fall back; only a real cancellation propagates.
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Summarisation failed for {Url}; using source description", item.Url);
                return Fallback(item);
            }
        }
    }

    private static bool IsRateLimited(ClientResultException ex) => ex.Status is 429 or 503;

    private static TimeSpan? RetryAfter(ClientResultException ex)
    {
        if (ex.GetRawResponse() is PipelineResponse response &&
            response.Headers.TryGetValue("retry-after", out string? value) &&
            int.TryParse(value, out int seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(Math.Min(seconds, 60));
        }

        return null;
    }

    private static string BuildUserPrompt(NewsItem item)
    {
        string content = item.Description.Length > 0
            ? TextUtilities.Truncate(item.Description, 1200)
            : "(no description available — summarise from the title and source only)";

        return $"""
            Source: {item.SourceName}
            Title: {item.Title}
            URL: {item.Url}
            Content: {content}
            """;
    }

    private static string Fallback(NewsItem item) =>
        item.Description.Length > 0 ? TextUtilities.Truncate(item.Description, 240) : item.Title;
}
