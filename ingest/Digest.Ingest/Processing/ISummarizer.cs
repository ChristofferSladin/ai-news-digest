using Digest.Ingest.Model;

namespace Digest.Ingest.Processing;

/// <summary>Produces a short, factual summary for a single item.</summary>
public interface ISummarizer
{
    /// <summary>
    /// Returns a 1–2 sentence summary. Implementations must not throw for transient model
    /// failures; they should degrade gracefully (e.g. fall back to the source description).
    /// </summary>
    Task<string> SummarizeAsync(NewsItem item, CancellationToken cancellationToken);
}
