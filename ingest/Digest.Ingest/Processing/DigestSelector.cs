using Digest.Ingest.Configuration;
using Digest.Ingest.Model;
using Microsoft.Extensions.Options;

namespace Digest.Ingest.Processing;

/// <summary>
/// Shapes the final digest from the scored pool: a narrow "scored items → balanced, ordered
/// list" surface that hides the cap/floor/guarantee/tie-break policy. Guarantees, per the
/// configured knobs: Local LLMs never exceed <see cref="IngestOptions.LocalLlmCap"/>; Research and
/// Agent systems each reach their floor whenever that many qualifying (≥ MinScore) items exist;
/// nothing below MinScore is ever included; the result never exceeds <see cref="IngestOptions.MaxItems"/>.
/// </summary>
public sealed class DigestSelector(IOptions<IngestOptions> options)
{
    public IReadOnlyList<NewsItem> Select(IReadOnlyList<NewsItem> scored)
    {
        IngestOptions opt = options.Value;

        // Eligible items, best first: highest score, then most recent.
        List<NewsItem> pool = scored
            .Where(i => i.Score >= opt.MinScore)
            .OrderByDescending(i => i.Score)
            .ThenByDescending(i => i.PublishedAt ?? DateTimeOffset.MinValue)
            .ToList();

        var chosen = new List<NewsItem>(opt.MaxItems);

        // Floors first, so they are guaranteed even when out-scored by other buckets.
        TakeFloor(pool, chosen, Category.Research, opt.ResearchFloor, opt.MaxItems);
        TakeFloor(pool, chosen, Category.AgentSystems, opt.AgentSystemsFloor, opt.MaxItems);

        // Fill the remaining slots by score, respecting the Local LLM cap.
        int localLlmCount = chosen.Count(i => i.Category == Category.LocalLlm);
        foreach (NewsItem item in pool)
        {
            if (chosen.Count >= opt.MaxItems)
            {
                break;
            }

            if (chosen.Contains(item))
            {
                continue;
            }

            if (item.Category == Category.LocalLlm)
            {
                if (localLlmCount >= opt.LocalLlmCap)
                {
                    continue;
                }

                localLlmCount++;
            }

            chosen.Add(item);
        }

        return chosen
            .OrderByDescending(i => i.Score)
            .ThenByDescending(i => i.PublishedAt ?? DateTimeOffset.MinValue)
            .ToList();
    }

    private static void TakeFloor(
        List<NewsItem> pool, List<NewsItem> chosen, Category category, int floor, int maxItems)
    {
        foreach (NewsItem item in pool.Where(i => i.Category == category).Take(floor))
        {
            if (chosen.Count >= maxItems)
            {
                break;
            }

            chosen.Add(item);
        }
    }
}
