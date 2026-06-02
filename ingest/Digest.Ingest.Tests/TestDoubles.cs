using Digest.Ingest.Storage;
using Microsoft.Extensions.AI;

namespace Digest.Ingest.Tests;

/// <summary>A <see cref="TimeProvider"/> pinned to a fixed instant for deterministic tests.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary><see cref="IChatClient"/> stub driven by a supplied handler.</summary>
internal sealed class StubChatClient(Func<IEnumerable<ChatMessage>, Task<ChatResponse>> handler) : IChatClient
{
    public int Calls { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return handler(messages);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}

/// <summary>Records the statements and parameters sent to D1 without any network calls.</summary>
internal sealed class FakeD1Client : ID1Client
{
    public List<(string Sql, IReadOnlyList<object?> Parameters)> Calls { get; } = [];

    public int ChangesPerCall { get; set; } = 1;

    public Task<D1Outcome> QueryAsync(
        string sql, IReadOnlyList<object?> parameters, CancellationToken cancellationToken)
    {
        Calls.Add((sql, parameters));
        return Task.FromResult(new D1Outcome(ChangesPerCall, Calls.Count));
    }
}
