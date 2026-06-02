using Digest.Ingest.Model;

namespace Digest.Ingest.Sources;

/// <summary>
/// A source of news items. Implementations should be self-contained and resilient:
/// the pipeline isolates each one so a single failing source never aborts the run.
/// </summary>
public interface INewsSource
{
    /// <summary>Human-readable name used in logs and stored on each item.</summary>
    string Name { get; }

    Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken);
}
