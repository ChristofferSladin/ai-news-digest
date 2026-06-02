namespace Digest.Ingest.Configuration;

/// <summary>
/// Credentials and identifiers for writing to Cloudflare D1 over the HTTP REST API.
/// All three values are secrets supplied via the environment; none are committed.
/// </summary>
public sealed class CloudflareD1Options
{
    public const string SectionName = "CloudflareD1";

    /// <summary>Cloudflare account id (<c>CF_ACCOUNT_ID</c>).</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>D1 database id (<c>D1_DATABASE_ID</c>).</summary>
    public string DatabaseId { get; set; } = string.Empty;

    /// <summary>Scoped API token with D1 edit permission (<c>CF_API_TOKEN</c>).</summary>
    public string ApiToken { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.cloudflare.com/client/v4/";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(DatabaseId) &&
        !string.IsNullOrWhiteSpace(ApiToken);

    /// <summary>Relative query endpoint for this database.</summary>
    public string QueryPath => $"accounts/{AccountId}/d1/database/{DatabaseId}/query";
}
