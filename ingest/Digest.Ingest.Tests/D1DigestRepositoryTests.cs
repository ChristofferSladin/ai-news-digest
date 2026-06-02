using Digest.Ingest.Model;
using Digest.Ingest.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Digest.Ingest.Tests;

public sealed class D1DigestRepositoryTests
{
    private static DigestItem Sample(string url) => new()
    {
        Date = "2026-06-01",
        Category = "ai-engineering",
        Title = "Title",
        Source = "Test",
        Url = url,
        Summary = "Summary",
        PublishedAt = "2026-06-01T04:00:00.0000000+00:00",
        Score = 12.5,
        CreatedAt = "2026-06-01T05:00:00.0000000+00:00",
    };

    [Fact]
    public async Task Upsert_issues_one_conflict_ignoring_insert_per_item()
    {
        var fake = new FakeD1Client();
        var repo = new D1DigestRepository(fake, NullLogger<D1DigestRepository>.Instance);

        int inserted = await repo.UpsertAsync(
            [Sample("https://example.com/a"), Sample("https://example.com/b")],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, inserted);
        Assert.Equal(2, fake.Calls.Count);
        foreach ((string sql, _) in fake.Calls)
        {
            Assert.StartsWith("INSERT INTO digest_item", sql, StringComparison.Ordinal);
            Assert.Contains("ON CONFLICT(url) DO NOTHING", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Parameters_are_bound_positionally_in_column_order()
    {
        var fake = new FakeD1Client();
        var repo = new D1DigestRepository(fake, NullLogger<D1DigestRepository>.Instance);

        await repo.UpsertAsync([Sample("https://example.com/a")], TestContext.Current.CancellationToken);

        IReadOnlyList<object?> p = fake.Calls[0].Parameters;
        Assert.Equal(9, p.Count);
        Assert.Equal("2026-06-01", p[0]);          // date
        Assert.Equal("ai-engineering", p[1]);      // category
        Assert.Equal("https://example.com/a", p[4]); // url
        Assert.Equal(12.5, p[7]);                  // score (numeric, not stringified)
        Assert.Equal("2026-06-01T05:00:00.0000000+00:00", p[8]); // created_at
    }

    [Fact]
    public async Task Returns_zero_when_all_rows_conflict()
    {
        var fake = new FakeD1Client { ChangesPerCall = 0 };
        var repo = new D1DigestRepository(fake, NullLogger<D1DigestRepository>.Instance);

        int inserted = await repo.UpsertAsync(
            [Sample("https://example.com/a")], TestContext.Current.CancellationToken);

        Assert.Equal(0, inserted);
    }
}
