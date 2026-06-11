namespace Digest.Ingest.Configuration;

/// <summary>Tunables for fetching and ranking. Sensible defaults; overridable via config.</summary>
public sealed class IngestOptions
{
    public const string SectionName = "Ingest";

    /// <summary>Maximum number of items kept after ranking.</summary>
    public int MaxItems { get; set; } = 15;

    /// <summary>Per-request HTTP timeout for source fetches.</summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>User-Agent sent on all outbound requests. A browser-like prefix avoids bot blocks (Reddit/Anthropic).</summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/124.0 Safari/537.36 solarm2m-digest/1.0 (+https://solarm2m.com)";

    /// <summary>Minimum Hacker News points for a story to be considered.</summary>
    public int HackerNewsMinPoints { get; set; } = 25;

    /// <summary>How far back to look on Hacker News.</summary>
    public int HackerNewsLookbackHours { get; set; } = 36;

    /// <summary>How many recent arXiv entries to pull per category before title filtering.</summary>
    public int ArxivMaxResults { get; set; } = 60;

    /// <summary>How many articles to take from each Anthropic section (news, engineering).</summary>
    public int AnthropicMaxPerSection { get; set; } = 6;

    /// <summary>Drop items whose relevance score is below this floor, even if slots remain.</summary>
    public double MinScore { get; set; } = 1.0;

    /// <summary>Maximum number of Local LLM items in the final digest (keeps that bucket from dominating).</summary>
    public int LocalLlmCap { get; set; } = 3;

    /// <summary>Guaranteed minimum Research items, pulled in over score when that many qualify (≥ MinScore).</summary>
    public int ResearchFloor { get; set; } = 3;

    /// <summary>Guaranteed minimum Agent systems items, pulled in over score when that many qualify (≥ MinScore).</summary>
    public int AgentSystemsFloor { get; set; } = 1;
}
