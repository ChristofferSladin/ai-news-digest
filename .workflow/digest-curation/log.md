# Run log: digest-curation

## Nightshift  (2026-06-11)
Base: `main` · night branch: `night/digest-curation` (planning docs @ 5a0a51a) · toolchain .NET 10.0.300 / Node 25.9.0

### Iteration 1 — ready-set {T0}
- **T0** "Agent systems" category → **done**. `ticket/T0` @ `430d51a`.
  - acceptance: `dotnet test --filter "Category|Categorizer"` 11/11 · full 49/49 · `npm --prefix web run build` pass.
  - decisions: enum doc comment "five"→"the digest groups"; shared keywords bucket to AgentSystems among non-pinned items (AgentSystems first in Priority); source-pin precedence deferred to T2.

### Iteration 2 — ready-set {T1, T2}  (parallel, worktrees off ticket/T0)
- worktrees `/tmp/digest-T1` (ticket/T1), `/tmp/digest-T2` (ticket/T2) — disjoint files, ran concurrently.
- **T2** Categorizer precedence → **done**. `ticket/T2` @ `c7309b9`. acceptance: `--filter Categorizer` 15/15 · full 53/53.
  - decisions: agent-comms pre-pin check at top of Categorize(); removed redundant AgentSystems Priority entry; hoisted SearchText.
- **T1** DigestSelector → **done**. `ticket/T1` @ `4fc2b8b`. acceptance: `--filter DigestSelector` 6/6 · full 55/55 · build clean.
  - decisions: knobs as fields on IngestOptions (board-allowed); floors-first algorithm; reference-identity Contains.

### Iteration 3 — ready-set {T3}
- T3 base assembled: `ticket/T3` off ticket/T1, merged ticket/T2 (clean, 19d1d84).
- **T3** end-to-end composition test → **done**. `ticket/T3` @ `06285c0`. acceptance: full suite **60/60** green.
  - decisions: deterministic per-title scores; single integration test; renamed a filler title to avoid a Research-keyword mis-bucket.
  - live dry-run (158 raw → 15): LocalLLM 3 (cap) · Research 5 · Agent systems 4 · .NET/Azure 1 · AI eng 2.

### Iteration 4 — ready-set {} → loop drained.

## Merge report  (/merge-night)
- `qa/digest-curation` created off `main`; merged `ticket/T0,T1,T2,T3` in DAG order.
- **All clean — zero conflicts** (disjoint files; T3 already integrated T1+T2). Nothing held back.
- Integration gate on qa: full suite **60/60 green**; `npm --prefix web run build` green.
- Diffstat vs main: 14 files, **+672 / −11**.
- `main` untouched; nothing pushed. → handed to `/qa-plan` (qa.md).
