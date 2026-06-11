using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using Digest.Ingest.Configuration;
using Digest.Ingest.Infrastructure;
using Digest.Ingest.Pipeline;
using Digest.Ingest.Processing;
using Digest.Ingest.Sources;
using Digest.Ingest.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// --- Configuration ---------------------------------------------------------
// Defaults come from the options classes. Env vars / user secrets override.
// The well-known flat secret names from CI map onto the typed option paths.
builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: false);
builder.Configuration.AddInMemoryCollection(MapSecretEnvironmentVariables());

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<CloudflareD1Options>(builder.Configuration.GetSection(CloudflareD1Options.SectionName));
builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection(IngestOptions.SectionName));

// --- Logging ---------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});
// Keep the run log focused on the pipeline; the HttpClient factory is very chatty at Information.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

// --- Core services ---------------------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InterestProfile>();
builder.Services.AddSingleton<RelevanceScorer>();
builder.Services.AddSingleton<Categorizer>();
builder.Services.AddSingleton<DigestSelector>();
builder.Services.AddSingleton<ISummarizer, GeminiSummarizer>();
builder.Services.AddSingleton<IDigestRepository, D1DigestRepository>();
builder.Services.AddSingleton<IngestRunner>();

// --- HTTP clients ----------------------------------------------------------
builder.Services.AddHttpClient(HttpClients.Feeds, (sp, client) =>
{
    IngestOptions o = sp.GetRequiredService<IOptions<IngestOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.HttpTimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
    client.DefaultRequestHeaders.Accept.ParseAdd(
        "application/atom+xml, application/rss+xml, application/xml;q=0.9, application/json, text/html;q=0.8, */*;q=0.5");
});

builder.Services.AddHttpClient<ID1Client, D1Client>((sp, client) =>
{
    CloudflareD1Options o = sp.GetRequiredService<IOptions<CloudflareD1Options>>().Value;
    client.BaseAddress = new Uri(o.BaseUrl);
    if (!string.IsNullOrEmpty(o.ApiToken))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiToken);
    }

    client.Timeout = TimeSpan.FromSeconds(30);
});

// --- Summarisation: IChatClient over the OpenAI-compatible Gemini endpoint --
builder.Services.AddSingleton<IChatClient>(sp =>
{
    GeminiOptions o = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    var openAiOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(o.Endpoint),
        // Disable the SDK's own transient retry so our 429-aware retry in GeminiSummarizer is the
        // single, quota-friendly source of truth (the SDK default would multiply requests per call).
        RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
    };
    // Placeholder key keeps construction valid in --dry-run (the client is never called then).
    string apiKey = string.IsNullOrWhiteSpace(o.ApiKey) ? "unconfigured" : o.ApiKey;
    var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), openAiOptions);
    return openAiClient.GetChatClient(o.Model).AsIChatClient();
});

// --- News sources ----------------------------------------------------------
foreach (FeedDefinition feed in FeedCatalog.Feeds)
{
    builder.Services.AddSingleton<INewsSource>(sp => new SyndicationSource(
        feed,
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<SyndicationSource>>()));
}

builder.Services.AddSingleton<INewsSource, HackerNewsSource>();
builder.Services.AddSingleton<INewsSource, ArxivSource>();
builder.Services.AddSingleton<INewsSource, AnthropicSource>();

using IHost host = builder.Build();
ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

bool dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));

// Fail fast on missing secrets with an actionable message (dry runs need none).
if (!dryRun)
{
    string? configError = ValidateConfiguration(host.Services);
    if (configError is not null)
    {
        logger.LogError("Configuration error: {Message}. See the README for the required secrets.", configError);
        return 2;
    }
}
else
{
    logger.LogInformation("Dry-run mode: fetching and ranking only (no summarisation, no D1 writes).");
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    IngestRunner runner = host.Services.GetRequiredService<IngestRunner>();
    await runner.RunAsync(dryRun, cts.Token);
    return 0;
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    logger.LogWarning("Ingest run cancelled.");
    return 130;
}
catch (Exception ex)
{
    // Anything else (including a stray timeout that escaped) is a real failure, not a cancel.
    logger.LogError(ex, "Ingest run failed.");
    return 1;
}

static IEnumerable<KeyValuePair<string, string?>> MapSecretEnvironmentVariables()
{
    KeyValuePair<string, string?>[] mappings =
    [
        new("Gemini:ApiKey", Environment.GetEnvironmentVariable("GEMINI_API_KEY")),
        new("CloudflareD1:ApiToken", Environment.GetEnvironmentVariable("CF_API_TOKEN")),
        new("CloudflareD1:AccountId", Environment.GetEnvironmentVariable("CF_ACCOUNT_ID")),
        new("CloudflareD1:DatabaseId", Environment.GetEnvironmentVariable("D1_DATABASE_ID")),
    ];

    return mappings.Where(m => !string.IsNullOrEmpty(m.Value));
}

static string? ValidateConfiguration(IServiceProvider services)
{
    GeminiOptions gemini = services.GetRequiredService<IOptions<GeminiOptions>>().Value;
    CloudflareD1Options d1 = services.GetRequiredService<IOptions<CloudflareD1Options>>().Value;

    var missing = new List<string>();
    if (!gemini.IsConfigured)
    {
        missing.Add("GEMINI_API_KEY");
    }

    if (string.IsNullOrWhiteSpace(d1.AccountId))
    {
        missing.Add("CF_ACCOUNT_ID");
    }

    if (string.IsNullOrWhiteSpace(d1.DatabaseId))
    {
        missing.Add("D1_DATABASE_ID");
    }

    if (string.IsNullOrWhiteSpace(d1.ApiToken))
    {
        missing.Add("CF_API_TOKEN");
    }

    return missing.Count == 0 ? null : $"missing required secrets: {string.Join(", ", missing)}";
}

/// <summary>Explicit partial Program type so tests and user-secrets can reference it.</summary>
public partial class Program;
