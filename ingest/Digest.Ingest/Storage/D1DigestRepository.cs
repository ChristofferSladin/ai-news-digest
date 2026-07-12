using Digest.Ingest.Model;
using Microsoft.Extensions.Logging;

namespace Digest.Ingest.Storage;

/// <summary>Writes <see cref="DigestItem"/>s to the D1 <c>digest_item</c> table via the REST client.</summary>
internal sealed class D1DigestRepository(ID1Client client, ILogger<D1DigestRepository> logger) : IDigestRepository
{
    private const string InsertSql =
        "INSERT INTO digest_item " +
        "(date, category, title, source, url, summary, published_at, score, created_at) " +
        "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?) " +
        "ON CONFLICT(url) DO NOTHING;";

    private const string PurgeSql = "DELETE FROM digest_item WHERE date < ?;";

    public async Task<int> UpsertAsync(IReadOnlyList<DigestItem> items, CancellationToken cancellationToken)
    {
        int inserted = 0;
        foreach (DigestItem item in items)
        {
            object?[] parameters =
            [
                item.Date,
                item.Category,
                item.Title,
                item.Source,
                item.Url,
                item.Summary,
                item.PublishedAt,
                item.Score,
                item.CreatedAt,
            ];

            D1Outcome outcome = await client.QueryAsync(InsertSql, parameters, cancellationToken);
            inserted += outcome.Changes;
        }

        logger.LogInformation("D1: inserted {Inserted} new of {Total} candidate items", inserted, items.Count);
        return inserted;
    }

    public async Task<int> PurgeOlderThanAsync(string cutoffDateExclusive, CancellationToken cancellationToken)
    {
        D1Outcome outcome = await client.QueryAsync(PurgeSql, [cutoffDateExclusive], cancellationToken);
        logger.LogInformation("D1: purged {Deleted} item(s) older than {Cutoff}", outcome.Changes, cutoffDateExclusive);
        return outcome.Changes;
    }
}
