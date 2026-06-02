using Digest.Ingest.Model;

namespace Digest.Ingest.Sources;

/// <summary>Definition of a single RSS/Atom feed source.</summary>
/// <param name="Name">Display name stored on items and shown in logs.</param>
/// <param name="Url">Absolute feed URL.</param>
/// <param name="CategoryHint">Optional default category for items from this feed.</param>
public sealed record FeedDefinition(string Name, string Url, Category? CategoryHint);

/// <summary>The static list of plain RSS/Atom feeds. API/scrape sources live in their own classes.</summary>
internal static class FeedCatalog
{
    public static readonly IReadOnlyList<FeedDefinition> Feeds =
    [
        new FeedDefinition(".NET Blog", "https://devblogs.microsoft.com/dotnet/feed/", Category.DotNetAzure),
        new FeedDefinition("Simon Willison", "https://simonwillison.net/atom/everything/", Category.AiEngineering),
        new FeedDefinition("Latent Space", "https://www.latent.space/feed", Category.AiEngineering),
        // Reddit blocks the JSON API for unauthenticated clients but serves the Atom feed.
        new FeedDefinition("r/LocalLLaMA", "https://www.reddit.com/r/LocalLLaMA/top.rss?t=day&limit=25", Category.LocalLlm),
    ];
}
