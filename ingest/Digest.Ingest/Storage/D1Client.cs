using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Digest.Ingest.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Digest.Ingest.Storage;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper over the Cloudflare D1 REST query endpoint.
/// Base address and bearer auth are configured by DI; this class owns request shaping,
/// transient retry with backoff, and response validation.
/// </summary>
internal sealed class D1Client(
    HttpClient httpClient,
    IOptions<CloudflareD1Options> options,
    ILogger<D1Client> logger) : ID1Client
{
    private const int MaxAttempts = 4;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public async Task<D1Outcome> QueryAsync(
        string sql, IReadOnlyList<object?> parameters, CancellationToken cancellationToken)
    {
        string path = options.Value.QueryPath;
        var payload = new D1QueryRequest(sql, parameters);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using HttpResponseMessage response =
                    await httpClient.PostAsJsonAsync(path, payload, JsonOptions, cancellationToken);

                if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                {
                    await BackoffAsync(attempt, response.StatusCode.ToString(), cancellationToken);
                    continue;
                }

                return await ReadOutcomeAsync(response, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested
                                       && attempt < MaxAttempts)
            {
                await BackoffAsync(attempt, ex.GetType().Name, cancellationToken);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private async Task BackoffAsync(int attempt, string reason, CancellationToken cancellationToken)
    {
        TimeSpan delay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
        logger.LogWarning("D1 request transient failure ({Reason}); retry {Attempt}/{Max} in {Delay}ms",
            reason, attempt, MaxAttempts, delay.TotalMilliseconds);
        await Task.Delay(delay, cancellationToken);
    }

    private static async Task<D1Outcome> ReadOutcomeAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new D1Exception($"D1 HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {Trim(body)}");
        }

        D1ApiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<D1ApiResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new D1Exception($"D1 response was not valid JSON: {ex.Message}");
        }

        if (parsed is null || !parsed.Success)
        {
            string errors = parsed?.Errors is { Count: > 0 } e
                ? string.Join("; ", e.Select(x => $"{x.Code}: {x.Message}"))
                : Trim(body);
            throw new D1Exception($"D1 query failed: {errors}");
        }

        D1QueryResult? result = parsed.Result is { Count: > 0 } ? parsed.Result[0] : null;
        return new D1Outcome(result?.Meta?.Changes ?? 0, result?.Meta?.LastRowId ?? 0);
    }

    private static string Trim(string body) => body.Length > 500 ? body[..500] : body;

    private sealed record D1QueryRequest(
        [property: JsonPropertyName("sql")] string Sql,
        [property: JsonPropertyName("params")] IReadOnlyList<object?> Params);

    private sealed record D1ApiResponse(
        [property: JsonPropertyName("result")] IReadOnlyList<D1QueryResult>? Result,
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("errors")] IReadOnlyList<D1Error>? Errors);

    private sealed record D1QueryResult(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("meta")] D1Meta? Meta);

    private sealed record D1Meta(
        [property: JsonPropertyName("changes")] int Changes,
        [property: JsonPropertyName("last_row_id")] long LastRowId);

    private sealed record D1Error(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string? Message);
}
