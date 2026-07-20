namespace Digest.Ingest.Configuration;

/// <summary>
/// Summarisation settings. The provider is reached through its OpenAI-compatible
/// endpoint and consumed via <c>Microsoft.Extensions.AI.IChatClient</c>, so swapping
/// providers is a matter of endpoint + model + key. Since 2026-07 the provider is the
/// LlmProxy service (alias <c>news-digest</c>), which fronts NVIDIA NIM with failover —
/// not Gemini; the class keeps its historical name to keep the swap config-only.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// API key. Supplied via the <c>GEMINI_API_KEY</c> secret; never committed. Carries the
    /// LlmProxy inbound key for this app (sent as <c>Authorization: Bearer</c>).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The proxy alias this app is keyed for; routing/model choice lives proxy-side.</summary>
    public string Model { get; set; } = "news-digest";

    /// <summary>OpenAI-compatible base endpoint (must end with a trailing slash).</summary>
    public string Endpoint { get; set; } = "https://llmproxy-app.azurewebsites.net/v1/";

    /// <summary>Delay between summary calls to stay within free-tier requests-per-minute limits.</summary>
    public int DelayBetweenCallsMs { get; set; } = 8_000;

    /// <summary>
    /// How many times to wait out a 429 and retry a single summary before falling back.
    /// Kept small: this only helps a transient per-minute blip. A daily-quota 429 cannot be
    /// retried away, so retrying harder there just burns more quota and time.
    /// </summary>
    public int MaxRateLimitRetries { get; set; } = 2;

    /// <summary>Wait before retrying a rate-limited (429) summary when no Retry-After header is given.</summary>
    public int RateLimitRetryDelayMs { get; set; } = 15_000;

    public int MaxOutputTokens { get; set; } = 200;

    public float Temperature { get; set; } = 0.2f;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
