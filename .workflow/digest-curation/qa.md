# QA: digest-curation   (night of 2026-06-11, branch `qa/digest-curation` off `main`)

The night drained the whole board autonomously (T0 → T1 ∥ T2 → T3), the merger assembled
`qa/digest-curation` with **zero conflicts**, and the integration suite is **60/60 green**.
`main` is untouched. Approve = merge `qa/digest-curation` → `main`.

## ⚖️ Decisions the night made — bless or redirect

1. **Agent-systems is "greedy" by keyword (T2 precedence).**
   Agent-comms keywords are checked BEFORE the source pins, and the set shares broad terms
   (`mcp`, `tool use`, `function calling`, `langgraph`/`autogen`/…) with AI-engineering — so any
   item mentioning them now buckets to **Agent systems**, including arXiv papers and r/LocalLLaMA
   posts. Today's live dry-run put **4 of 15** items in Agent systems (an arXiv "Agentic… Survey"
   re-bucketed from Research, an MCP .NET-blog post, etc.). This is exactly the "agent-comms wins"
   precedence you chose — but 4/15 may be broader than you pictured for "at least one".
   - [ ] OK as-is   [ ] Tighten: drop the broad terms (`tool use`/`function calling`) or require a
     **title** match for agent-comms → a smaller, higher-precision Agent-systems bucket.

2. **Knobs on `IngestOptions`, not a separate policy type (T1).** `LocalLlmCap=3`, `ResearchFloor=3`,
   `AgentSystemsFloor=1`, all config-overridable under `Ingest`.  [ ] OK   [ ] Change: ____

3. **Floors can displace higher-scoring items (T1, by design).** A guaranteed research/agent item can
   bump a higher-scoring filler out of the 15 — the whole point, but worth knowing.  [ ] OK   [ ] Change

4. Minor: T0 fixed the enum doc comment ("five"→"the digest groups"); T2 removed the now-redundant
   Priority entry; T3 used deterministic test scores.  [ ] OK

## Review (newest first)

### T3 — End-to-end composition test   ·  afk  ·  ✅ 60/60
- **Claims:** proves categorise + select compose into the right ~15.
- **Diff:** `+CompositionTests.cs` (141)  · commit `06285c0`
- **Acceptance:** `dotnet test ingest/Digest.Ingest.slnx` → 60/60 green.
- **Decisions:** deterministic per-title scores (immune to scorer tuning); one integration test.
- **Manual QA:** `cd ~/RiderProjects/ai-news-digest && dotnet run --project ingest/Digest.Ingest -- --dry-run` → eyeball the breakdown (LocalLLM ≤3, Research strong, ≥1 Agent systems).
- [ ] Approve   [ ] Request changes: ____

### T2 — Agent-comms categorisation wins over source pins   ·  afk  ·  ✅ 15/15
- **Claims:** an agent-comms keyword match beats the arXiv→Research / r/LocalLLaMA pins.
- **Diff:** `Categorizer.cs` (+pre-pin check, −redundant Priority entry) · `CategorizerTests.cs` (+4)  · commit `c7309b9`
- **Acceptance:** `dotnet test --filter Categorizer` → 15/15 green.
- **Manual QA:** in the dry-run preview, confirm an MCP/agent item shows under "Agent systems" (see decision #1 re breadth).
- [ ] Approve   [ ] Request changes: ____

### T1 — Balanced selection policy (DigestSelector)   ·  afk  ·  ✅ 6/6
- **Claims:** replaces pure top-N with caps/floors/guarantee (LocalLLM ≤3, Research ≥3, Agent systems ≥1).
- **Diff:** `+DigestSelector.cs` (79) · `IngestOptions.cs` (+9) · `IngestRunner.cs` (delegates) · `Program.cs` (+DI) · `+DigestSelectorTests.cs` (134)  · commit `4fc2b8b`
- **Acceptance:** `dotnet test --filter DigestSelector` → 6/6 green.
- **Manual QA:** read `DigestSelector.Select` (floors-first, then fill-by-score under the cap); confirm dry-run LocalLLM count == 3.
- [ ] Approve   [ ] Request changes: ____

### T0 — "Agent systems" category, threaded end-to-end   ·  afk  ·  ✅ 11/11 + web build
- **Claims:** new category through enum → keywords → categorizer → stored slug → frontend.
- **Diff:** `Category.cs` · `InterestProfile.cs` · `Categorizer.cs` · `categories.ts` (+green `#4f9d69`) · `CategorizerTests.cs`  · commit `430d51a`
- **Acceptance:** `dotnet test --filter "Category|Categorizer"` 11/11 + `npm --prefix web run build` pass.
- **Manual QA:** `npm --prefix web run dev` and confirm an "Agent systems" chip renders green once data exists (or just check `categories.ts`).
- [ ] Approve   [ ] Request changes: ____

## Integration
- Full suite on `qa/digest-curation`: **60/60 green** (all four slices together). Web build: **green**.
- Held back from merge: **none** — all four branches merged cleanly (disjoint files).
- Live dry-run composition (158 raw → 15 kept): **LocalLLM 3 · Research 5 · Agent systems 4 · .NET/Azure 1 · AI eng 2**.

## How to act
- **Approve the feature** → `git checkout main && git merge qa/digest-curation` (you do this — agents never push). It takes effect on the next ingest run: the dry-run does NOT write to D1; the scheduled GitHub Actions "Daily ingest" (or a real `dotnet run` with secrets) writes the new composition, and the PWA then shows the "Agent systems" section.
- **Request changes** (most likely: tune decision #1's keyword breadth) → I'll amend the board with a small follow-up ticket and re-run `/nightshift`.
- After approval: this repo has no `PRODUCT.md`; consider a one-line note in the README "Sources/Ranking" section about the caps/floors + the new category.

## Cleanup
Night artifacts on disk: branches `ticket/T0..T3`, worktrees `/tmp/digest-T1,2,3` and `/tmp/digest-qa`. Say the word and I'll `git worktree remove` them and delete the ticket branches (after you merge, or to discard).
