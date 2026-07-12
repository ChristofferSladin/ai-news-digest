using Digest.Ingest.Configuration;
using Digest.Ingest.Model;
using Digest.Ingest.Pipeline;
using Digest.Ingest.Processing;
using Digest.Ingest.Sources;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Digest.Ingest.Tests;

/// <summary>
/// Orchestration tests for the T2 retention purge: <see cref="IngestRunner.RunAsync"/> must call
/// through to <see cref="IDigestRepository.PurgeOlderThanAsync"/> once per run — including on the
/// zero-kept-items early-return path — with a cutoff of (this run's digest date − 30 days), and
/// it must NOT purge at all during a dry run (existing contract: dry run makes no writes).
/// </summary>
public sealed class IngestRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 5, 0, 0, TimeSpan.Zero);
    private const string ExpectedCutoff = "2026-06-12"; // Now's date, minus 30 days

    /// <summary>Scores well above MinScore — mirrors RelevanceScorerTests' "highly relevant" case.</summary>
    private static NewsItem RelevantItem(string url) => new()
    {
        Title = "Microsoft.Extensions.AI and RAG with Semantic Kernel",
        Url = url,
        SourceName = "Test",
        PublishedAt = Now,
    };

    /// <summary>Scores exactly zero — mirrors RelevanceScorerTests' "irrelevant item" case.</summary>
    private static NewsItem IrrelevantItem(string url) => new()
    {
        Title = "Sourdough baking tips",
        Description = "how to grow tomatoes",
        Url = url,
        SourceName = "Test",
    };

    private static IngestRunner BuildRunner(IReadOnlyList<NewsItem> sourceItems, FakeDigestRepository repository)
    {
        var options = Options.Create(new IngestOptions());
        var geminiOptions = Options.Create(new GeminiOptions());
        var interests = new InterestProfile();
        var clock = new FixedTimeProvider(Now);
        var source = new FakeNewsSource("Test", sourceItems);

        return new IngestRunner(
            [source],
            new RelevanceScorer(interests, clock),
            new Categorizer(interests),
            new DigestSelector(options),
            new FakeSummarizer(),
            repository,
            options,
            geminiOptions,
            clock,
            NullLogger<IngestRunner>.Instance);
    }

    [Fact]
    public async Task Purge_runs_with_cutoff_30_days_before_the_run_s_digest_date()
    {
        var repository = new FakeDigestRepository();
        IngestRunner runner = BuildRunner([RelevantItem("https://example.com/a")], repository);

        await runner.RunAsync(dryRun: false, TestContext.Current.CancellationToken);

        Assert.Single(repository.PurgeCalls);
        Assert.Equal(ExpectedCutoff, repository.PurgeCalls[0]);
    }

    [Fact]
    public async Task Purge_still_runs_when_zero_items_are_kept()
    {
        var repository = new FakeDigestRepository();
        IngestRunner runner = BuildRunner([IrrelevantItem("https://example.com/b")], repository);

        int result = await runner.RunAsync(dryRun: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, result);
        Assert.Empty(repository.UpsertCalls);
        Assert.Single(repository.PurgeCalls);
        Assert.Equal(ExpectedCutoff, repository.PurgeCalls[0]);
    }

    [Fact]
    public async Task Purge_does_not_run_during_a_dry_run()
    {
        var repository = new FakeDigestRepository();
        IngestRunner runner = BuildRunner([RelevantItem("https://example.com/c")], repository);

        await runner.RunAsync(dryRun: true, TestContext.Current.CancellationToken);

        Assert.Empty(repository.PurgeCalls);
        Assert.Empty(repository.UpsertCalls);
    }

    [Fact]
    public async Task Purge_does_not_run_during_a_dry_run_even_with_zero_kept_items()
    {
        var repository = new FakeDigestRepository();
        IngestRunner runner = BuildRunner([IrrelevantItem("https://example.com/d")], repository);

        await runner.RunAsync(dryRun: true, TestContext.Current.CancellationToken);

        Assert.Empty(repository.PurgeCalls);
        Assert.Empty(repository.UpsertCalls);
    }
}
