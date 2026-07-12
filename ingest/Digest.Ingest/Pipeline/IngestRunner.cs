using System.Diagnostics;
using System.Globalization;
using Digest.Ingest.Configuration;
using Digest.Ingest.Model;
using Digest.Ingest.Processing;
using Digest.Ingest.Sources;
using Digest.Ingest.Storage;
using Digest.Ingest.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Digest.Ingest.Pipeline;

/// <summary>
/// Orchestrates one ingest run: fetch every source (isolating failures), dedupe, score,
/// rank, summarise, group and upsert. Progress is logged so the Actions run is the record.
/// </summary>
internal sealed class IngestRunner(
    IEnumerable<INewsSource> sources,
    RelevanceScorer scorer,
    Categorizer categorizer,
    DigestSelector selector,
    ISummarizer summarizer,
    IDigestRepository repository,
    IOptions<IngestOptions> ingestOptions,
    IOptions<GeminiOptions> geminiOptions,
    TimeProvider clock,
    ILogger<IngestRunner> logger)
{
    private readonly IReadOnlyList<INewsSource> _sources = sources.ToList();

    /// <summary>Rolling retention window: rows older than this many days (vs. the run's digest date) are purged.</summary>
    private const int RetentionDays = 30;

    /// <summary>
    /// Runs the pipeline and returns the number of newly stored items. In <paramref name="dryRun"/>
    /// mode it fetches, dedupes, scores and ranks, prints a preview, then stops — no model calls,
    /// no writes — so it needs no secrets.
    /// </summary>
    public async Task<int> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        DateTimeOffset now = clock.GetUtcNow();
        string digestDate = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        IngestOptions opt = ingestOptions.Value;

        logger.LogInformation("Ingest run starting for {Date} (UTC) across {Sources} sources",
            digestDate, _sources.Count);

        List<NewsItem> fetched = await FetchAllAsync(cancellationToken);
        List<NewsItem> deduped = Deduplicate(fetched);

        foreach (NewsItem item in deduped)
        {
            item.Category = categorizer.Categorize(item);
            item.Score = scorer.Score(item);
        }

        List<NewsItem> kept = selector.Select(deduped).ToList();

        logger.LogInformation("Ranked {Deduped} items, kept top {Kept} (min score {Min})",
            deduped.Count, kept.Count, opt.MinScore);

        if (kept.Count == 0)
        {
            logger.LogWarning("No items met the relevance threshold; nothing to store.");
            if (!dryRun)
            {
                await PurgeOldItemsAsync(now, cancellationToken);
            }

            return 0;
        }

        LogCategoryBreakdown(kept);

        if (dryRun)
        {
            LogRankedPreview(kept);
            logger.LogInformation("Dry run complete in {Elapsed:n1}s: {Count} item(s) would be summarised and stored",
                stopwatch.Elapsed.TotalSeconds, kept.Count);
            return kept.Count;
        }

        await SummarizeAllAsync(kept, cancellationToken);

        string createdAt = now.ToString("O", CultureInfo.InvariantCulture);
        List<DigestItem> digestItems = kept.Select(i => new DigestItem
        {
            Date = digestDate,
            Category = i.Category.ToSlug(),
            Title = i.Title,
            Source = i.SourceName,
            Url = i.Url,
            Summary = i.AiSummary,
            PublishedAt = i.PublishedAt?.ToString("O", CultureInfo.InvariantCulture),
            Score = Math.Round(i.Score, 2),
            CreatedAt = createdAt,
        }).ToList();

        int inserted = await repository.UpsertAsync(digestItems, cancellationToken);
        await PurgeOldItemsAsync(now, cancellationToken);

        logger.LogInformation("Ingest run complete in {Elapsed:n1}s: {Inserted} new item(s) stored for {Date}",
            stopwatch.Elapsed.TotalSeconds, inserted, digestDate);
        return inserted;
    }

    /// <summary>
    /// Purges rows older than a rolling <see cref="RetentionDays"/>-day window, anchored on this
    /// run's digest date. Unconditional except for dry-run, which the caller must guard for —
    /// dry-run makes no writes of any kind (existing contract).
    /// </summary>
    private async Task PurgeOldItemsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        string cutoff = now.AddDays(-RetentionDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await repository.PurgeOlderThanAsync(cutoff, cancellationToken);
    }

    private async Task<List<NewsItem>> FetchAllAsync(CancellationToken cancellationToken)
    {
        IEnumerable<Task<IReadOnlyList<NewsItem>>> tasks = _sources.Select(async source =>
        {
            try
            {
                return await source.FetchAsync(cancellationToken);
            }
            // Catch everything except a genuine cancellation of our own token. Crucially this
            // includes TaskCanceledException from HTTP timeouts (a subclass of
            // OperationCanceledException) so one slow source never aborts the run.
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Source {Source} failed; continuing without it", source.Name);
                return [];
            }
        });

        IReadOnlyList<NewsItem>[] results = await Task.WhenAll(tasks);
        List<NewsItem> all = results.SelectMany(r => r).ToList();
        logger.LogInformation("Fetched {Count} raw items in total", all.Count);
        return all;
    }

    private List<NewsItem> Deduplicate(IReadOnlyList<NewsItem> items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NewsItem>(items.Count);
        foreach (NewsItem item in items)
        {
            if (seen.Add(TextUtilities.NormalizeUrl(item.Url)))
            {
                result.Add(item);
            }
        }

        int removed = items.Count - result.Count;
        if (removed > 0)
        {
            logger.LogInformation("Removed {Removed} duplicate item(s) by URL", removed);
        }

        return result;
    }

    private async Task SummarizeAllAsync(IReadOnlyList<NewsItem> items, CancellationToken cancellationToken)
    {
        int delayMs = geminiOptions.Value.DelayBetweenCallsMs;
        for (int index = 0; index < items.Count; index++)
        {
            NewsItem item = items[index];
            item.AiSummary = await summarizer.SummarizeAsync(item, cancellationToken);
            logger.LogInformation("Summarised [{Index}/{Total}] ({Score:n1}) {Title}",
                index + 1, items.Count, item.Score, TextUtilities.Truncate(item.Title, 70));

            if (delayMs > 0 && index < items.Count - 1)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }

    private void LogCategoryBreakdown(IReadOnlyList<NewsItem> items)
    {
        foreach (IGrouping<Category, NewsItem> group in items.GroupBy(i => i.Category).OrderBy(g => g.Key))
        {
            logger.LogInformation("  {Category}: {Count} item(s)", group.Key.ToLabel(), group.Count());
        }
    }

    private void LogRankedPreview(IReadOnlyList<NewsItem> items)
    {
        logger.LogInformation("--- Ranked preview ({Count} items, no summaries) ---", items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            NewsItem item = items[i];
            logger.LogInformation("{Rank,2}. [{Score,5:n1}] {Category,-20} {Title}  ·  {Source}",
                i + 1, item.Score, item.Category.ToLabel(), TextUtilities.Truncate(item.Title, 80), item.SourceName);
        }
    }
}
