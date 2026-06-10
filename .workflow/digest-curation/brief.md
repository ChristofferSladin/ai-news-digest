# Brief: digest-curation — rebalance ranking + add "Agent systems" category   (repo: ai-news-digest, base: main)

## What the human asked (raw)
"i want to rank relevant news from the sources that are present in the repo. i have noticed that
local llm news are alot of the cca 15 news i get everyday. im more interested in arxiv research,
and i want the news to be at least one (if possible) to be about communication between agents and
tools for big tasks."

## Current state vs desired (the diff)
- **Now:** the ingest pipeline scores each item (weighted keyword hits ×2 in title / ×1 in body +
  recency boost), then selects a pure global **top-15 by score**. Local-LLM is favoured three ways:
  high keyword weights (ollama/local llm = 5, llama.cpp/gguf/vllm = 4), a dedicated high-volume
  source (r/LocalLLaMA), and many matchable terms per item. No category caps, floors, or guaranteed
  slots. arXiv only enters if its title matches a scoring keyword, and pure-research signal words
  aren't scoring keywords, so research is easily crowded out. Agent-comms items have no home category
  and no guaranteed slot.
- **Want:** replace the pure top-N with a balanced selection that caps local-LLM, floors research,
  and guarantees ≥1 agent-comms item, plus a new visible "Agent systems" category that takes
  precedence over the arXiv→Research pin. Selection-only — no scoring-weight, intake, or schema changes.

## Acceptance criteria (testable)
- [ ] A new DigestSelector replaces the inline top-N; given a scored pool it returns ≤MaxItems
      honouring LocalLlm ≤3, Research ≥3 (when ≥3 clear MinScore), AgentSystems ≥1 (when any clears
      MinScore), filling the rest by score, ties by recency — verified by:
      `dotnet test ingest/Digest.Ingest.slnx` (new DigestSelectorTests)
- [ ] New AgentSystems category exists end-to-end (enum + slug `agent-systems` + label "Agent
      systems") — verified by: `dotnet test ingest/Digest.Ingest.slnx`
- [ ] Categorizer buckets agent-comms items into AgentSystems, taking precedence over the
      arXiv→Research and r/LocalLLaMA pins (a multi-agent arXiv title → AgentSystems) — verified by:
      `dotnet test` (extended CategorizerTests)
- [ ] The agent-comms keyword set lives in InterestProfile as the single source of truth — verified
      by: `dotnet test`
- [ ] Frontend renders the new category with label + colour (agent-systems → green #4f9d69); build
      passes — verified by: `npm --prefix web run build`
- [ ] Dry-run prints a ranked preview whose category breakdown honours the caps/floors on the day's
      real data — verified by: `dotnet run --project ingest/Digest.Ingest -- --dry-run`
- [ ] Re-running a day still upserts ON CONFLICT DO NOTHING; existing rows/categories untouched —
      verified by: inspection of repository upsert + existing D1DigestRepositoryTests

## Non-goals / out of scope
- No scoring-weight changes (selection-only)
- No new sources; no change to arXiv intake / title filter
- No D1 schema migration (category is free-text TEXT)
- No summariser changes; MaxItems stays 15; other categories unchanged
- Read-API logic unchanged (it already passes category through)

## Blast radius
- Fair game: `ingest/Digest.Ingest` (Category, InterestProfile, Categorizer, new DigestSelector,
  IngestRunner, IngestOptions, Program.cs DI, tests) + `web/src/categories.ts`
- Off limits: `migrations/`, `Sources/`, the summariser, scoring weights, `functions/api` read-API logic

## Vertical slice (core flow, top to bottom)
Category enum/slug/label → InterestProfile agent-comms keyword set → Categorizer (agent-comms
precedence over pins) → DigestSelector (caps/floors/guarantee policy) → IngestRunner wiring +
IngestOptions knobs → D1 stores the slug (no migration) → read API passthrough (no change) →
frontend categories.ts label + colour

## Constraints
- Free-tier Gemini: ~15 items/day stays within limits; selection runs before summarisation so cost stays flat
- One failing source never aborts a run (existing invariant; unaffected)
- DigestSelector must be deterministic and unit-testable in isolation (prior art: RelevanceScorerTests)

## Edge cases & failure modes
- Fewer than a floor available (e.g. <3 research clear MinScore) → take what exists; never pad below MinScore
- An item matching both agent-comms and local-LLM keywords → AgentSystems wins (checked first), so it
  does not consume the local-LLM cap
- Local-LLM items fewer than the cap → no effect
- Total scored pool < MaxItems → return what exists
- Ties on score → break by recency (existing ThenByDescending(PublishedAt))

## Decisions log (resolved during grilling)
- Q: soft (weights) vs hard (selection policy) lever? → A: balanced selection policy (can actually guarantee)
- Q: target mix of the ~15? → A: LocalLlm ≤3, Research ≥3, AgentSystems ≥1, rest by score
- Q: agent-comms as hidden tag or visible category? → A: new visible "Agent systems" category
- Q: arXiv-about-agents — Research or Agent systems? → A: agent-comms wins; category == the guarantee
- Agent-comms keyword set (confirmed): multi-agent, agent-to-agent / a2a, agent communication,
  agent orchestration, agent handoff, multi-agent collaboration, agent protocol, mcp / model context
  protocol, tool use, function calling, task decomposition, long-horizon, langgraph, autogen, crewai, swarm

## Contradictions found vs the code
- Task: "prioritize arxiv research." Code: arXiv is title-filtered to scoring keywords and pure-research
  signal words aren't scoring keywords, so research underscores. Resolution: selection-only floors
  guarantee the research items that DO clear MinScore get slots over local-LLM; manufacturing more
  research (intake/weights) is explicitly out of scope.
- Task: "at least one agent-comms item." Code: no guarantee mechanism; pure top-N. Resolution:
  AgentSystems floor ≥1 in DigestSelector, satisfiable only when such an item clears MinScore.
