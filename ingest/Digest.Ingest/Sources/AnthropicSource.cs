using System.Net;
using System.Text.RegularExpressions;
using Digest.Ingest.Configuration;
using Digest.Ingest.Infrastructure;
using Digest.Ingest.Model;
using Digest.Ingest.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Digest.Ingest.Sources;

/// <summary>
/// Anthropic has no public feed, so we scrape the server-rendered article links from the
/// /news and /engineering listings, then read each article's <c>og:title</c>. Every fetch
/// is isolated: a failed article falls back to a slug-derived title, a failed section is
/// skipped.
/// </summary>
internal sealed partial class AnthropicSource(
    IHttpClientFactory httpClientFactory,
    IOptions<IngestOptions> options,
    ILogger<AnthropicSource> logger) : INewsSource
{
    private const string BaseUrl = "https://www.anthropic.com";

    private static readonly (string Path, Category Hint)[] Sections =
    [
        ("/news", Category.AiEngineering),
        ("/engineering", Category.AiEngineering),
    ];

    public string Name => "Anthropic";

    [GeneratedRegex("<meta\\s+property=\"og:title\"\\s+content=\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitlePattern();

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken)
    {
        IngestOptions opt = options.Value;
        HttpClient client = httpClientFactory.CreateClient(HttpClients.Feeds);
        using var concurrency = new SemaphoreSlim(4);
        var items = new List<NewsItem>();

        foreach ((string path, Category hint) in Sections)
        {
            try
            {
                string listing = await client.GetStringAsync(BaseUrl + path, cancellationToken);
                var slugPattern = new Regex(
                    $"href=\"{Regex.Escape(path)}/([a-z0-9][a-z0-9-]*)\"", RegexOptions.IgnoreCase);

                List<string> slugs = slugPattern.Matches(listing)
                    .Select(m => m.Groups[1].Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(opt.AnthropicMaxPerSection)
                    .ToList();

                // Fetch article titles with bounded concurrency; Task.WhenAll preserves order.
                NewsItem[] sectionItems = await Task.WhenAll(slugs.Select(async slug =>
                {
                    await concurrency.WaitAsync(cancellationToken);
                    try
                    {
                        string url = $"{BaseUrl}{path}/{slug}";
                        return new NewsItem
                        {
                            Title = await ResolveTitleAsync(client, url, slug, cancellationToken),
                            Url = url,
                            SourceName = Name,
                            CategoryHint = hint,
                        };
                    }
                    finally
                    {
                        concurrency.Release();
                    }
                }));

                items.AddRange(sectionItems);
                logger.LogInformation("Anthropic {Path}: collected {Count} articles", path, sectionItems.Length);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Anthropic: failed to fetch section {Path}", path);
            }
        }

        return items;
    }

    private async Task<string> ResolveTitleAsync(
        HttpClient client, string url, string slug, CancellationToken cancellationToken)
    {
        try
        {
            string html = await client.GetStringAsync(url, cancellationToken);
            Match match = OgTitlePattern().Match(html);
            if (match.Success)
            {
                string title = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                if (title.Length > 0)
                {
                    return title;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Anthropic: could not read title for {Url}; using slug", url);
        }

        return TextUtilities.SlugToTitle(slug);
    }
}
