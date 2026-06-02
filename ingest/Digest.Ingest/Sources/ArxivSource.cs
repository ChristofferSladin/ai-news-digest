using Digest.Ingest.Configuration;
using Digest.Ingest.Infrastructure;
using Digest.Ingest.Model;
using Digest.Ingest.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Digest.Ingest.Sources;

/// <summary>
/// Recent cs.AI + cs.CL submissions from the arXiv Atom API, title-filtered to the
/// interest profile (the firehose is far too broad to keep wholesale).
/// </summary>
internal sealed class ArxivSource(
    IHttpClientFactory httpClientFactory,
    IOptions<IngestOptions> options,
    InterestProfile interests,
    ILogger<ArxivSource> logger) : INewsSource
{
    public string Name => "arXiv";

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken)
    {
        IngestOptions opt = options.Value;
        HttpClient client = httpClientFactory.CreateClient(HttpClients.Feeds);

        string url = "https://export.arxiv.org/api/query?search_query=" +
                     "cat:cs.AI+OR+cat:cs.CL&sortBy=submittedDate&sortOrder=descending" +
                     $"&start=0&max_results={opt.ArxivMaxResults}";

        using HttpResponseMessage response = await client.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var buffer = new MemoryStream();
        await using (Stream content = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            await content.CopyToAsync(buffer, cancellationToken);
        }

        buffer.Position = 0;
        IReadOnlyList<NewsItem> all = FeedParser.Parse(buffer, Name, Category.Research);

        List<NewsItem> matched = all
            .Where(item => interests.MatchesAny(item.Title.ToLowerInvariant()))
            .ToList();

        logger.LogInformation("arXiv: {Matched}/{Total} recent entries matched the interest profile",
            matched.Count, all.Count);
        return matched;
    }
}
