-- Sample data for LOCAL frontend development only (never used in production).
-- Apply after `npm run migrate:local`:  npm run seed:local
-- Idempotent: ON CONFLICT(url) DO NOTHING means re-seeding is safe.

INSERT INTO digest_item (date, category, title, source, url, summary, published_at, score, created_at) VALUES
  ('2026-06-01', 'dotnet-azure', 'Announcing the Microsoft Agent Framework GA for .NET', '.NET Blog',
   'https://example.dev/dotnet/agent-framework-ga',
   'The Microsoft Agent Framework reaches general availability for .NET, unifying Semantic Kernel and AutoGen primitives behind a single agent abstraction.',
   '2026-06-01T08:10:00.0000000+00:00', 28.0, '2026-06-01T05:00:00.0000000+00:00'),
  ('2026-06-01', 'ai-engineering', 'Practical evals for retrieval-augmented agents', 'Latent Space',
   'https://example.dev/latent/practical-evals-rag',
   'A walkthrough of building lightweight eval harnesses for RAG agents, covering grounding checks, retrieval precision and regression tracking.',
   '2026-06-01T06:40:00.0000000+00:00', 24.5, '2026-06-01T05:00:00.0000000+00:00'),
  ('2026-06-01', 'domain', 'Structured extraction from invoices with small models', 'Hacker News',
   'https://example.dev/hn/invoice-extraction',
   'A team shares how they replaced a brittle OCR pipeline with structured extraction from financial documents using a fine-tuned small model.',
   '2026-06-01T04:20:00.0000000+00:00', 22.0, '2026-06-01T05:00:00.0000000+00:00'),
  ('2026-06-01', 'local-llms', 'Running Qwen3 locally with Ollama on 16GB', 'r/LocalLLaMA',
   'https://example.dev/reddit/qwen3-ollama-16gb',
   'Benchmarks and quantization tips for running Qwen3 via Ollama and llama.cpp on consumer hardware with 16GB of VRAM.',
   '2026-06-01T03:05:00.0000000+00:00', 21.5, '2026-06-01T05:00:00.0000000+00:00'),
  ('2026-06-01', 'research', 'Memory policies for long-horizon LLM agents', 'arXiv',
   'https://example.dev/arxiv/2606.01234',
   'The paper proposes a meta-cognitive memory policy that improves long-horizon agent task completion while reducing context window usage.',
   '2026-06-01T01:00:00.0000000+00:00', 19.0, '2026-06-01T05:00:00.0000000+00:00'),
  ('2026-05-31', 'dotnet-azure', 'Azure AI Foundry adds first-class Microsoft.Extensions.AI support', '.NET Blog',
   'https://example.dev/dotnet/foundry-extensions-ai',
   'Azure AI Foundry now exposes models directly through the Microsoft.Extensions.AI IChatClient abstraction, simplifying provider swaps in .NET apps.',
   '2026-05-31T09:15:00.0000000+00:00', 26.0, '2026-05-31T05:00:00.0000000+00:00'),
  ('2026-05-31', 'ai-engineering', 'A field guide to MCP tool design', 'Simon Willison',
   'https://example.dev/simon/mcp-tool-design',
   'Notes on designing Model Context Protocol tools that are legible to agents, with concrete naming and schema recommendations.',
   '2026-05-31T07:30:00.0000000+00:00', 20.0, '2026-05-31T05:00:00.0000000+00:00'),
  ('2026-05-31', 'local-llms', 'Flash attention speedups for llama.cpp on RDNA3', 'r/LocalLLaMA',
   'https://example.dev/reddit/flash-attention-rdna3',
   'A contributor reports a 47% reduction in KV-cache VRAM and measurable speedups for llama.cpp on AMD RDNA3 GPUs.',
   '2026-05-31T02:45:00.0000000+00:00', 18.0, '2026-05-31T05:00:00.0000000+00:00')
ON CONFLICT(url) DO NOTHING;
