namespace Digest.Ingest.Configuration;

/// <summary>
/// Summarisation settings. The provider is reached through its OpenAI-compatible
/// endpoint and consumed via <c>Microsoft.Extensions.AI.IChatClient</c>, so swapping
/// providers is a matter of endpoint + model + key.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>API key. Supplied via the <c>GEMINI_API_KEY</c> secret; never committed.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>OpenAI-compatible base endpoint (must end with a trailing slash).</summary>
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    /// <summary>Delay between summary calls to stay within free-tier requests-per-minute limits.</summary>
    public int DelayBetweenCallsMs { get; set; } = 6_000;

    public int MaxOutputTokens { get; set; } = 200;

    public float Temperature { get; set; } = 0.2f;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
