namespace Digest.Ingest.Model;

/// <summary>
/// A single fetched item flowing through the pipeline. Sources set the descriptive
/// fields; processing stages fill in <see cref="Category"/>, <see cref="Score"/> and
/// <see cref="AiSummary"/>.
/// </summary>
public sealed class NewsItem
{
    public required string Title { get; init; }

    /// <summary>Canonical article URL. Also the dedupe key (DB UNIQUE constraint).</summary>
    public required string Url { get; init; }

    /// <summary>Human-readable source name, e.g. ".NET Blog" or "Hacker News".</summary>
    public required string SourceName { get; init; }

    /// <summary>Original description/abstract from the source, plain text (may be empty).</summary>
    public string Description { get; init; } = string.Empty;

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Hint from the source about likely category; the categoriser may override.</summary>
    public Category? CategoryHint { get; init; }

    // --- Filled in by processing stages ---

    public Category Category { get; set; }

    public double Score { get; set; }

    /// <summary>1–2 sentence model-generated summary; falls back to a trimmed description on failure.</summary>
    public string AiSummary { get; set; } = string.Empty;

    /// <summary>Lower-cased title + description, used for scoring and categorisation.</summary>
    public string SearchText => $"{Title} {Description}".ToLowerInvariant();
}
