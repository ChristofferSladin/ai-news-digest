using Digest.Ingest.Infrastructure;
using Digest.Ingest.Model;
using Microsoft.Extensions.Logging;

namespace Digest.Ingest.Sources;

/// <summary>A news source backed by a single RSS/Atom feed URL.</summary>
internal sealed class SyndicationSource(
    FeedDefinition definition,
    IHttpClientFactory httpClientFactory,
    ILogger<SyndicationSource> logger) : INewsSource
{
    public string Name => definition.Name;

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient(HttpClients.Feeds);

        using HttpResponseMessage response = await client.GetAsync(
            definition.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // SyndicationFeed.Load is synchronous, so buffer the body first rather than
        // holding the socket open while parsing.
        using var buffer = new MemoryStream();
        await using (Stream content = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            await content.CopyToAsync(buffer, cancellationToken);
        }

        buffer.Position = 0;
        IReadOnlyList<NewsItem> items = FeedParser.Parse(buffer, Name, definition.CategoryHint);
        logger.LogInformation("{Source}: fetched {Count} items", Name, items.Count);
        return items;
    }
}
