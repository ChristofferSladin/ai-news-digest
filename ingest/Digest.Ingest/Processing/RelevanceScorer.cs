using Digest.Ingest.Model;

namespace Digest.Ingest.Processing;

/// <summary>
/// Scores an item against the interest profile. Title matches count double, body matches
/// once, each keyword counts at most once, and recent items get a freshness boost.
/// </summary>
public sealed class RelevanceScorer(InterestProfile interests, TimeProvider clock)
{
    public double Score(NewsItem item)
    {
        string title = item.Title.ToLowerInvariant();
        string body = item.Description.ToLowerInvariant();

        double score = 0;
        foreach (WeightedKeyword wk in interests.ScoringKeywords)
        {
            if (wk.Keyword.Matches(title))
            {
                score += wk.Weight * 2.0;
            }
            else if (body.Length > 0 && wk.Keyword.Matches(body))
            {
                score += wk.Weight;
            }
        }

        if (item.PublishedAt is { } published)
        {
            double ageHours = (clock.GetUtcNow() - published).TotalHours;
            score += ageHours switch
            {
                < 24 => 3.0,
                < 72 => 1.5,
                < 168 => 0.5,
                _ => 0.0,
            };

            if (ageHours > 24 * 30)
            {
                score -= 2.0; // demote anything older than ~a month that slipped through
            }
        }

        return Math.Max(0, score);
    }
}
