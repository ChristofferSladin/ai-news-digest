## Board: digest-curation   (base: main)

| id | title                                              | type | status | blocked-by |
|----|----------------------------------------------------|------|--------|------------|
| T0 | "Agent systems" category, threaded end-to-end      | afk  | todo   | —          |
| T1 | Balanced selection policy (DigestSelector)         | afk  | todo   | T0         |
| T2 | Agent-comms categorisation wins over source pins   | afk  | todo   | T0         |
| T3 | End-to-end composition test + dry-run check        | afk  | todo   | T1, T2     |

```
DAG:        ┌── T1 ──┐
   T0 ──────┤        ├── T3
            └── T2 ──┘
   (T1 ∥ T2 after T0; they touch disjoint files)
```

---

### T0 — "Agent systems" category, threaded end-to-end
- **type:** afk
- **status:** todo
- **blocked-by:** —
- **module:** `Category` (enum/slug/label) + the agent-comms keyword set in `InterestProfile` — the seam every other slice plugs into.
- **slice:** add `Category.AgentSystems` (slug `agent-systems`, label "Agent systems") → add the confirmed agent-comms keyword set to `InterestProfile` as the AgentSystems vocabulary → `Categorizer` recognises AgentSystems via those keywords (add to the priority list; NOT yet the source-pin precedence — that's T2) → an item flows through the existing pipeline and stores the new slug → the frontend renders it with a label + colour. Existing selection unchanged.
- **acceptance-check:** `dotnet test ingest/Digest.Ingest.slnx --filter "Category|Categorizer"` green AND `npm --prefix web run build` passes. New test: a non-arXiv agent-comms item (title e.g. "A protocol for multi-agent collaboration") → `Category.AgentSystems`; slug/label round-trip.
- **files-likely-touched:** `ingest/Digest.Ingest/Model/Category.cs` · `Processing/InterestProfile.cs` · `Processing/Categorizer.cs` · `ingest/Digest.Ingest.Tests/CategorizerTests.cs` (+ a small Category slug/label test) · `web/src/categories.ts`
- **decisions:** (none yet)
- **notes:** colour `#4f9d69` (green; distinct from the 5 existing). Keep AgentSystems off the source-pin path here so T2 owns that change alone (collision-free).

### T1 — Balanced selection policy (DigestSelector)
- **type:** afk
- **status:** todo
- **blocked-by:** T0
- **module:** `DigestSelector` — a narrow "scored items + policy → final ordered list" surface hiding the cap/floor/guarantee/tie-break algorithm.
- **slice:** new `DigestSelector` + `SelectionPolicy` → add knobs to `IngestOptions` (LocalLlmCap=3, ResearchFloor=3, AgentSystemsFloor=1; MaxItems/MinScore already exist) → `IngestRunner` delegates the final selection to it instead of the inline LINQ → register in `Program.cs` DI. Algorithm: order by score then recency; take greedily but stop adding LocalLlm past the cap; back-fill Research and AgentSystems up to their floors from the next-highest qualifying items (≥MinScore); truncate to MaxItems; guarantees displace the lowest-scoring non-guaranteed items.
- **acceptance-check:** `dotnet test ingest/Digest.Ingest.slnx --filter DigestSelector` green. Tests assert: LocalLlm capped at 3; Research floored at 3 when ≥3 exist; ≥1 AgentSystems when one exists; floors never padded below MinScore; ties broken by recency; total ≤ MaxItems.
- **files-likely-touched:** `ingest/Digest.Ingest/Processing/DigestSelector.cs` (new) · `Processing/SelectionPolicy.cs` (new, or fields on IngestOptions) · `Configuration/IngestOptions.cs` · `Pipeline/IngestRunner.cs` · `Program.cs` · `ingest/Digest.Ingest.Tests/DigestSelectorTests.cs` (new)
- **decisions:** (none yet)
- **notes:** disjoint from T2's files — safe to run in parallel with T2.

### T2 — Agent-comms categorisation wins over source pins
- **type:** afk
- **status:** todo
- **blocked-by:** T0
- **module:** `Categorizer` — same public surface (item → Category), new precedence rule.
- **slice:** in `Categorizer`, check the agent-comms keyword set FIRST, before the arXiv→Research and r/LocalLLaMA `CategoryHint` pins, so any matching item (including arXiv) buckets to AgentSystems.
- **acceptance-check:** `dotnet test ingest/Digest.Ingest.slnx --filter Categorizer` green. Tests assert: an arXiv-hinted item with a multi-agent title → AgentSystems (not Research); a LocalLlm-hinted item that also matches agent-comms → AgentSystems (not LocalLlm); a plain local-llm item still → LocalLlm.
- **files-likely-touched:** `ingest/Digest.Ingest/Processing/Categorizer.cs` · `ingest/Digest.Ingest.Tests/CategorizerTests.cs`
- **decisions:** (none yet)
- **notes:** builds on T0's keyword set + enum. Disjoint from T1.

### T3 — End-to-end composition test + dry-run check
- **type:** afk
- **status:** todo
- **blocked-by:** T1, T2
- **module:** an integration test exercising `Categorizer` + `DigestSelector` together — the tracer bullet hitting its target.
- **slice:** one behavioural test that runs a synthetic NewsItem set through categorisation + selection and asserts the final composition (≤3 local-llm, ≥3 research, ≥1 agent-systems, ≤15, the agent arXiv item present under AgentSystems). Plus a documented dry-run for the human.
- **acceptance-check:** `dotnet test ingest/Digest.Ingest.slnx` all green (incl. the new composition test) AND `dotnet run --project ingest/Digest.Ingest -- --dry-run` prints a ranked preview whose category breakdown honours the caps/floors.
- **files-likely-touched:** `ingest/Digest.Ingest.Tests/CompositionTests.cs` (new) — uses existing TestDoubles
- **decisions:** (none yet)
- **notes:** confirms T1 and T2 compose correctly; the all-green run is the merge gate.
