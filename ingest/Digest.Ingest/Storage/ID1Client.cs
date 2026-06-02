namespace Digest.Ingest.Storage;

/// <summary>Result metadata from a D1 write.</summary>
/// <param name="Changes">Number of rows actually changed (0 when an upsert hits a conflict).</param>
/// <param name="LastRowId">Row id of the last insert, when applicable.</param>
public sealed record D1Outcome(int Changes, long LastRowId);

/// <summary>Thin client over the Cloudflare D1 HTTP query API. Statements are parameterised.</summary>
public interface ID1Client
{
    /// <summary>
    /// Executes a single parameterised statement. Throws <see cref="D1Exception"/> on an API-level
    /// failure after exhausting transient retries.
    /// </summary>
    Task<D1Outcome> QueryAsync(string sql, IReadOnlyList<object?> parameters, CancellationToken cancellationToken);
}

/// <summary>Raised when the D1 API reports failure or an unexpected response shape.</summary>
public sealed class D1Exception(string message) : Exception(message);
