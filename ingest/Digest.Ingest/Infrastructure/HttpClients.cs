namespace Digest.Ingest.Infrastructure;

/// <summary>Named <see cref="System.Net.Http.IHttpClientFactory"/> client identifiers.</summary>
internal static class HttpClients
{
    /// <summary>Outbound client for fetching feeds, APIs and scraped pages (browser-like User-Agent).</summary>
    public const string Feeds = "feeds";

    /// <summary>Client for the Cloudflare D1 REST API (bearer auth, JSON).</summary>
    public const string D1 = "d1";
}
