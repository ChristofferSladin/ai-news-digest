using Digest.Ingest.Model;

namespace Digest.Ingest.Processing;

/// <summary>A weighted interest keyword used for relevance scoring.</summary>
public sealed record WeightedKeyword(Keyword Keyword, double Weight);

/// <summary>
/// The single source of truth for "what this user cares about": weighted keywords for
/// ranking, and per-category keyword sets for bucketing. Registered as a singleton and
/// shared by the scorer, the categoriser, and the arXiv title filter.
/// </summary>
public sealed class InterestProfile
{
    public IReadOnlyList<WeightedKeyword> ScoringKeywords { get; }

    public IReadOnlyDictionary<Category, IReadOnlyList<Keyword>> CategoryKeywords { get; }

    public InterestProfile()
    {
        ScoringKeywords = BuildScoringKeywords();
        CategoryKeywords = BuildCategoryKeywords();
    }

    /// <summary>True if any scoring keyword appears in the (lower-cased) text. Used to title-filter arXiv.</summary>
    public bool MatchesAny(string lowerText)
    {
        foreach (WeightedKeyword wk in ScoringKeywords)
        {
            if (wk.Keyword.Matches(lowerText))
            {
                return true;
            }
        }

        return false;
    }

    private static List<WeightedKeyword> BuildScoringKeywords()
    {
        // Weight reflects how strongly a term signals *this* user's specific interests.
        (string Term, double Weight)[] raw =
        [
            // .NET + Microsoft AI stack — the strongest signals.
            ("microsoft.extensions.ai", 6), ("semantic kernel", 6), ("agent framework", 6),
            ("azure ai foundry", 5), ("azure openai", 5), ("azure ai", 4),
            (".net", 4), ("c#", 4), ("asp.net", 3), ("blazor", 3), ("dotnet", 4), (".net aspire", 4),

            // Document intelligence / financial extraction — a named focus area.
            ("document intelligence", 6), ("structured extraction", 6), ("financial document", 6),
            ("information extraction", 4), ("invoice", 4),

            // RAG / agents / evals.
            ("rag", 5), ("retrieval augmented", 5), ("retrieval-augmented", 5),
            ("agentic", 4), ("agents", 4), ("agent", 3),
            ("evals", 4), ("eval", 3), ("evaluation", 2),
            ("tool use", 3), ("function calling", 3), ("mcp", 4), ("model context protocol", 4),
            ("embedding", 3), ("embeddings", 3), ("vector", 2), ("retrieval", 3),

            // Local LLMs / Ollama.
            ("ollama", 5), ("local llm", 5), ("local model", 4), ("llama.cpp", 4), ("gguf", 4),
            ("vllm", 4), ("lm studio", 4), ("quantization", 3), ("quantized", 3), ("open-weight", 3),
            ("open weights", 3), ("on-device", 3),

            // Domain: accounting / fintech / enterprise.
            ("accounting", 5), ("fintech", 5), ("bookkeeping", 5), ("erp", 4), ("ledger", 4),
            ("reconciliation", 4), ("audit", 3), ("compliance", 3), ("enterprise", 2),

            // General model/tooling vocabulary — lower weight to avoid drowning the specifics.
            ("llm", 3), ("large language model", 3), ("fine-tune", 3), ("fine-tuning", 3),
            ("inference", 2), ("prompt", 2), ("context window", 3), ("copilot", 3),
            ("claude", 2), ("anthropic", 2), ("openai", 2), ("gemini", 2), ("llama", 2),
            ("mistral", 2), ("qwen", 2), ("deepseek", 2), ("phi", 2),
        ];

        return raw.Select(r => new WeightedKeyword(new Keyword(r.Term), r.Weight)).ToList();
    }

    private static Dictionary<Category, IReadOnlyList<Keyword>> BuildCategoryKeywords()
    {
        static IReadOnlyList<Keyword> K(params string[] terms) =>
            terms.Select(t => new Keyword(t)).ToList();

        return new Dictionary<Category, IReadOnlyList<Keyword>>
        {
            [Category.LocalLlm] = K(
                "ollama", "local llm", "local model", "llama.cpp", "gguf", "vllm", "lm studio",
                "quantization", "quantized", "open-weight", "open weights", "on-device", "self-host",
                "self-hosted", "localllama"),

            [Category.DotNetAzure] = K(
                ".net", "c#", "asp.net", "blazor", "dotnet", "azure", "semantic kernel",
                "microsoft.extensions.ai", "agent framework", "azure openai", "azure ai foundry",
                "nuget", "visual studio", "maui", "aspire"),

            [Category.Domain] = K(
                "accounting", "fintech", "bookkeeping", "erp", "ledger", "reconciliation", "audit",
                "compliance", "invoice", "financial document", "tax", "expense", "kyc", "fraud", "bank"),

            [Category.Research] = K(
                "arxiv", "benchmark", "state-of-the-art", "we propose", "we introduce", "dataset",
                "fine-tuning", "pretraining", "transformer"),

            [Category.AiEngineering] = K(
                "rag", "agent", "agents", "agentic", "eval", "evals", "embedding", "embeddings",
                "vector", "prompt", "retrieval", "tool use", "function calling", "mcp", "inference",
                "context window", "fine-tune"),
        };
    }
}
