using Digest.Ingest.Model;

namespace Digest.Ingest.Storage;

/// <summary>Persists digest items, deduping on URL so re-runs are idempotent.</summary>
public interface IDigestRepository
{
    /// <summary>
    /// Inserts each item, ignoring rows whose URL already exists
    /// (<c>ON CONFLICT(url) DO NOTHING</c>). Returns the number of newly inserted rows.
    /// </summary>
    Task<int> UpsertAsync(IReadOnlyList<DigestItem> items, CancellationToken cancellationToken);
}
