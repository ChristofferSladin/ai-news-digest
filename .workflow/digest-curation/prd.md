# PRD: digest-curation — rebalance ranking + "Agent systems" category

## Problem statement
The daily ~15-item digest is dominated by local-LLM news. The reader wants less local-LLM, more arXiv
research, and a guarantee that — whenever one is available — at least one item is about communication
between agents and tools for large/complex tasks. None of this is controllable today: items are chosen
by a pure global top-15-by-score, so whatever scores highest takes every slot, and there is no home for
"agent systems" content.

## Solution
Keep the existing scoring, but replace the final selection with a balanced policy that shapes the
composition of the ~15: cap local-LLM, floor research, and guarantee an agent-comms slot. Introduce a
visible "Agent systems" category so agent-communication items (including arXiv ones) are grouped and the
guarantee maps cleanly onto a category quota. All three asks then become one uniform category-quota
policy applied after scoring.

## User stories
1. As the reader, I want local-LLM items capped at a few per day, so the digest stops being dominated by them.
2. As the reader, I want research items floored, so arXiv work that cleared the bar isn't crowded out by higher-scoring local-LLM items.
3. As the reader, I want at least one agent-comms item whenever one is available, so I keep up with multi-agent / agent-tool coordination for big tasks.
4. As the reader, I want a visible "Agent systems" section, so those items are grouped and easy to find.
5. As the reader, I want an arXiv paper about multi-agent systems to appear under "Agent systems", so the topic is grouped consistently regardless of source.
6. As the reader, I want the rest of the digest still chosen by relevance, so quality elsewhere is unchanged.
7. As the maintainer, I want the selection logic isolated and unit-tested, so the composition rules are verifiable and safe to change.
8. As the maintainer, I want no schema migration or API change, so the change is low-risk and ships behind the existing pipeline.

## Acceptance criteria (definition of done)
- [ ] Balanced selection module replaces the inline top-N; given a scored pool it returns ≤MaxItems
      honouring LocalLlm ≤3, Research ≥3 (when ≥3 clear MinScore), AgentSystems ≥1 (when any clears
      MinScore), remaining slots by score, ties by recency — verified by: `dotnet test ingest/Digest.Ingest.slnx`
- [ ] "Agent systems" category exists end-to-end (enum + slug `agent-systems` + label "Agent systems") — verified by: `dotnet test`
- [ ] Agent-comms items are categorised AgentSystems, taking precedence over the arXiv→Research and r/LocalLLaMA source pins — verified by: `dotnet test`
- [ ] Agent-comms vocabulary is defined once in the interest profile — verified by: `dotnet test`
- [ ] Frontend shows the new category with a label + colour and the web build passes — verified by: `npm --prefix web run build`
- [ ] A dry-run prints a ranked preview whose category breakdown reflects the caps/floors — verified by: `dotnet run --project ingest/Digest.Ingest -- --dry-run`
- [ ] Re-running a day does not duplicate or mutate existing rows — verified by: existing repository tests + inspection

## Deep-module map
- **DigestSelector (new)** — interface: takes the scored, categorised items plus a selection policy and
  returns the final ordered list. Hides: the entire balancing algorithm (score ordering, the local-LLM
  cap, the research/agent-systems floors with back-fill, MaxItems truncation, tie-breaking). This is the
  deep module — a narrow "items in → items out" surface concealing all the composition rules. Tested:
  yes; prior art is the relevance-scorer's tests.
- **SelectionPolicy (new, small value)** — the knobs: MaxItems, MinScore, per-category caps and floors.
  Plain data, surfaced as ingest options. Tested: via the selector.
- **Category (modify)** — add the AgentSystems member with its slug and label; the single mapping shared
  with the read API and frontend. Tested: yes (slug/label round-trip).
- **InterestProfile (modify)** — add the agent-comms keyword set as the AgentSystems category vocabulary;
  remains the single source of truth for keywords. Tested: via the categoriser.
- **Categorizer (modify)** — same public surface (item → category) but a new precedence rule: agent-comms
  keywords decide AgentSystems before the source pins. Tested: yes (extended).
- **IngestRunner (modify)** — same behaviour, but delegates final selection to DigestSelector instead of
  inline LINQ. Tested: indirectly via the selector + a dry run.
- **Frontend category registry (modify)** — add the new slug's label + colour. Tested: web build.

## Data model / schema changes
None. The category is stored as a free-text slug, so a new value flows through storage and the read API
unchanged. Re-runs keep using upsert-on-conflict, so existing rows and their categories are untouched.

## Vertical slices (preview of tickets)
- **T0 walking skeleton:** the AgentSystems category threaded end-to-end (enum/slug/label → categoriser
  recognises it → a minimal selection keeps it → stored slug → frontend label+colour), proven by one test
  plus the web build. Establishes every seam the other slices plug into.
- **Selection policy slice:** the DigestSelector with the local-LLM cap, research floor, agent-systems
  floor, MaxItems and tie-breaking, wired into the runner and options.
- **Categorisation precedence slice:** agent-comms keywords win over the arXiv/Reddit source pins.
- **Verification slice:** dry-run composition check + the full suite green.

## Constraints & standing rules
- Selection-only: scoring weights, sources, and arXiv intake are untouched.
- Free-tier summariser cost stays flat (selection still yields ~15 items, summarised after selection).
- One failing source never aborts a run (existing invariant).
- The selector must be deterministic and unit-testable in isolation.

## Out of scope (non-goals)
- Changing scoring weights or adding research-signal scoring keywords.
- Adding or modifying sources, or relaxing the arXiv title filter.
- Any D1 schema migration.
- Summariser changes; changing MaxItems away from 15; touching other categories; changing read-API logic.

## Risks & open questions
- Risk: floors unsatisfiable on thin days (little research / no agent-comms) → mitigation: floors are
  best-effort above MinScore; never pad with noise; the digest simply has fewer of that category.
- Risk: the agent-comms keyword set is too broad (catches shallow "tool use" mentions) or too narrow (the
  guarantee rarely fires) → mitigation: start with the confirmed set; it lives in one place and is
  trivially tunable; QA eyeballs real output.
- Risk: pulling agent arXiv papers into AgentSystems thins the Research floor → accepted trade-off (the
  reader chose category == guarantee).

## Blast radius
- Fair game: the ingest app's model / processing / pipeline / configuration + its tests, and the frontend
  category registry.
- Off limits: D1 migrations, the news sources, the summariser, scoring weights, and the read-API logic.
