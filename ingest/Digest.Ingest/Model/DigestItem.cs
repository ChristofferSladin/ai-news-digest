namespace Digest.Ingest.Model;

/// <summary>
/// An item ready to persist to the <c>digest_item</c> table. Maps 1:1 to a row
/// (apart from the auto-increment id). Dates/timestamps are pre-formatted strings
/// so the storage layer stays free of formatting concerns.
/// </summary>
public sealed record DigestItem
{
    /// <summary>Digest date this item belongs to, ISO <c>yyyy-MM-dd</c> (UTC).</summary>
    public required string Date { get; init; }

    /// <summary>Category slug (see <see cref="Categories.ToSlug"/>).</summary>
    public required string Category { get; init; }

    public required string Title { get; init; }

    public required string Source { get; init; }

    public required string Url { get; init; }

    public required string Summary { get; init; }

    /// <summary>Original publish time as ISO-8601 (UTC), or null when unknown.</summary>
    public string? PublishedAt { get; init; }

    public required double Score { get; init; }

    /// <summary>Row creation time, ISO-8601 (UTC).</summary>
    public required string CreatedAt { get; init; }
}
