## Board: digest-curation   (base: main)   — ALL DONE ✓

| id | title                                              | type | status | blocked-by |
|----|----------------------------------------------------|------|--------|------------|
| T0 | "Agent systems" category, threaded end-to-end      | afk  | done   | —          |
| T1 | Balanced selection policy (DigestSelector)         | afk  | done   | T0         |
| T2 | Agent-comms categorisation wins over source pins   | afk  | done   | T0         |
| T3 | End-to-end composition test + dry-run check        | afk  | done   | T1, T2     |

```
DAG:        ┌── T1 ──┐
   T0 ──────┤        ├── T3        all green → qa/digest-curation (60/60)
            └── T2 ──┘
```

---

### T0 — "Agent systems" category, threaded end-to-end
- **type:** afk · **status:** done (`ticket/T0` @ 430d51a — filtered 11/11, full 49/49, web build pass)
- **blocked-by:** —
- **module:** `Category` + the agent-comms keyword set in `InterestProfile` — the seam everything plugs into.
- **acceptance-check:** `dotnet test --filter "Category|Categorizer"` + `npm --prefix web run build` — green.
- **decisions:** enum doc comment "five"→"the digest groups"; AgentSystems first in Priority; source-pin precedence left for T2; colour `#4f9d69`.

### T1 — Balanced selection policy (DigestSelector)
- **type:** afk · **status:** done (`ticket/T1` @ 4fc2b8b — DigestSelector 6/6, full 55/55)
- **blocked-by:** T0
- **module:** `DigestSelector` — narrow "scored items → final list", hides the caps/floors/guarantee algorithm.
- **acceptance-check:** `dotnet test --filter DigestSelector` — green.
- **decisions:** knobs as fields on `IngestOptions` (LocalLlmCap=3/ResearchFloor=3/AgentSystemsFloor=1); floors-first then fill-by-score under the cap; reference-identity de-dup.

### T2 — Agent-comms categorisation wins over source pins
- **type:** afk · **status:** done (`ticket/T2` @ c7309b9 — Categorizer 15/15, full 53/53)
- **blocked-by:** T0
- **module:** `Categorizer` — same surface (item → Category), new precedence rule.
- **acceptance-check:** `dotnet test --filter Categorizer` — green.
- **decisions:** agent-comms pre-pin check added at top of `Categorize()`; removed redundant Priority entry; hoisted `SearchText`. QA-flag: broad shared terms route items into Agent systems (4/15 in live dry-run) — bless or tighten (see qa.md #1).

### T3 — End-to-end composition test + dry-run check
- **type:** afk · **status:** done (`ticket/T3` @ 06285c0 — full suite **60/60**)
- **blocked-by:** T1, T2
- **module:** integration test exercising `Categorizer` + `DigestSelector` together.
- **acceptance-check:** `dotnet test ingest/Digest.Ingest.slnx` (all) + `dotnet run -- --dry-run` — green; live composition LocalLLM 3 / Research 5 / Agent systems 4 / .NET 1 / AI eng 2.
- **decisions:** deterministic per-title scores; one integration test; renamed a filler title to avoid a Research-keyword mis-bucket.
