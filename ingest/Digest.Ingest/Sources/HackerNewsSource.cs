using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Digest.Ingest.Configuration;
using Digest.Ingest.Infrastructure;
using Digest.Ingest.Model;
using Digest.Ingest.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Digest.Ingest.Sources;

/// <summary>
/// Recent, popular Hacker News stories via the Algolia search API. Pulls stories above
/// a points threshold from the recent window; relevance filtering is left to the scorer
/// so off-topic stories simply rank out.
/// </summary>
internal sealed class HackerNewsSource(
    IHttpClientFactory httpClientFactory,
    IOptions<IngestOptions> options,
    TimeProvider clock,
    ILogger<HackerNewsSource> logger) : INewsSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "Hacker News";

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken)
    {
        IngestOptions opt = options.Value;
        HttpClient client = httpClientFactory.CreateClient(HttpClients.Feeds);

        long since = clock.GetUtcNow().AddHours(-opt.HackerNewsLookbackHours).ToUnixTimeSeconds();
        string url = "https://hn.algolia.com/api/v1/search_by_date?tags=story&hitsPerPage=50" +
                     $"&numericFilters=created_at_i%3E{since},points%3E{opt.HackerNewsMinPoints}";

        AlgoliaResponse response =
            await client.GetFromJsonAsync<AlgoliaResponse>(url, JsonOptions, cancellationToken)
            ?? new AlgoliaResponse([]);

        var items = new List<NewsItem>(response.Hits.Count);
        foreach (Hit hit in response.Hits)
        {
            if (string.IsNullOrWhiteSpace(hit.Title))
            {
                continue;
            }

            string link = string.IsNullOrWhiteSpace(hit.Url)
                ? $"https://news.ycombinator.com/item?id={hit.ObjectId}"
                : hit.Url;

            string description = string.IsNullOrWhiteSpace(hit.StoryText)
                ? $"{hit.Points} points and {hit.NumComments} comments on Hacker News."
                : TextUtilities.StripHtml(hit.StoryText);

            items.Add(new NewsItem
            {
                Title = TextUtilities.Clean(hit.Title),
                Url = link,
                SourceName = Name,
                Description = description,
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds(hit.CreatedAtUnix),
                CategoryHint = Category.AiEngineering,
            });
        }

        logger.LogInformation("Hacker News: fetched {Count} stories above {Points} points in the last {Hours}h",
            items.Count, opt.HackerNewsMinPoints, opt.HackerNewsLookbackHours);
        return items;
    }

    private sealed record AlgoliaResponse(
        [property: JsonPropertyName("hits")] IReadOnlyList<Hit> Hits);

    private sealed record Hit(
        [property: JsonPropertyName("objectID")] string ObjectId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("points")] int Points,
        [property: JsonPropertyName("num_comments")] int NumComments,
        [property: JsonPropertyName("created_at_i")] long CreatedAtUnix,
        [property: JsonPropertyName("story_text")] string? StoryText);
}
