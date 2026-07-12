# Nightshift log: news-retention-full-load

## Run summary
Base commit for all tickets: `b59030f` (`workflow(news-retention-full-load): brief, prd, board`
on `main`). One iteration drained the board:

- **Iteration 1 -- ready-set {T0, T2}** (independent, dispatched in parallel, each in its own
  git worktree):
  - **T0** -- Read API uncapped history window -- `done`. Branch `ticket/T0`, commit `708b9bd`.
    Acceptance-check PASS (`ALL=35 CAPPED=3`).
  - **T2** -- Ingest 30-day retention purge -- `done`. Branch `ticket/T2`, commit `ff61ad0`.
    6/6 new Purge-filtered tests, 66/66 full suite green.
- **Iteration 2 -- ready-set {T1}** (unblocked once T0 went terminal; worktree branched from
  `ticket/T0` so the API contract it depends on was already present):
  - **T1** -- Frontend single load, drop infinite scroll -- `flagged` (hitl). Branch `ticket/T1`,
    commit `c88c139` on top of T0. Build PASS, grep confirms no paging remnants. Flagged
    because runtime network behavior (one fetch on load, zero on category clicks) has no
    automated harness in this repo -- left as an explicit manual-QA step.
- Ready-set empty after iteration 2. Board fully drained: T0 done, T1 flagged, T2 done.

## Decisions worth QA's attention
- T0: garbage/non-finite `days` values now clamp to `MAX_DAYS` (31) rather than being treated
  as uncapped -- uncapped only when the param is entirely absent.
- T2: retention window is a plain `RetentionDays = 30` constant on `IngestRunner`, not an
  `IOptions` knob; purge runs on the zero-kept-items path (guarded by `!dryRun`) and
  unconditionally on the normal path.
- T1: kept a `busyRef` re-entrancy guard in `useDigests` (unrelated to paging, prevents
  overlapping reloads); removed now-orphaned `.sentinel`/`.footnote` CSS alongside the deleted
  elements even though `styles.css` wasn't in the ticket's files-likely-touched list.

## Manual QA required (T1, hitl)
1. Run the app locally or against a Pages preview; open DevTools Network tab filtered on
   `digests`.
2. Fresh load: confirm exactly one `GET /api/digests` request (no query string), returning
   every digest day in the store.
3. Click every category chip and scroll well past where the old sentinel sat: confirm zero
   further `/api/digests` requests.
4. Confirm category filtering still narrows the visible list correctly (logic untouched, but
   worth a visual check).
5. Trigger the error view once and click Retry: confirm a single new request, not a duplicate
   pair.

---

## Merge report (merge-night)

**Base:** `main` @ `b59030f`. **QA branch:** `qa/news-retention-full-load`, worktree at
`.claude/worktrees/qa-news-retention-full-load`.

**Merge order and results (all clean, nothing held back):**
1. `ticket/T0` (`708b9bd`) -- fast-forward (qa branch had no divergence yet).
2. `ticket/T2` (`ff61ad0`) -- clean 3-way merge, no conflicts (disjoint files: ingest/* vs.
   T0's functions/api/digests.ts).
3. `ticket/T1` (`c88c139`, already contains T0 in its ancestry) -- clean 3-way merge, no
   conflicts (disjoint files: web/src/* vs. T2's ingest/*).

No conflicts of any kind across the three merges -- the kanban decomposition's "no shared
seam" call for T0/T2 held, and T1's ordering after T0 meant its diff never touched
`functions/api/digests.ts` itself.

**Integration suite on the assembled branch (beyond each ticket's own check):**
- `dotnet test ingest/Digest.Ingest.Tests` (full suite): **66/66 passed**, 0 failed.
- `npm run typecheck:functions`: clean, no errors.
- `npm --prefix web run build` (`tsc --noEmit && vite build`): clean build, PWA precache
  generated normally.
- Full-stack boot-and-probe re-verifying T0+T1's composed contract on the merged code: seeded
  10 distinct dates into local D1, booted `wrangler pages dev`, confirmed `GET /api/digests`
  with no params returns all 10 dates and `?days=2` returns exactly 2 -- the read-path vertical
  works end-to-end on the assembled branch, not just in each ticket's isolated worktree.

**Consolidated diffstat (`git diff b59030f...qa/news-retention-full-load --stat`):**
```
 functions/api/digests.ts                           |  48 +++++----
 ingest/Digest.Ingest.Tests/D1DigestRepositoryTests.cs |  27 +++++
 ingest/Digest.Ingest.Tests/IngestRunnerTests.cs    | 111 +++++++++++++++++++++
 ingest/Digest.Ingest.Tests/TestDoubles.cs          |  39 ++++++++
 ingest/Digest.Ingest/Pipeline/IngestRunner.cs      |  20 ++++
 ingest/Digest.Ingest/Storage/D1DigestRepository.cs |   9 ++
 ingest/Digest.Ingest/Storage/IDigestRepository.cs  |   7 ++
 web/src/App.tsx                                    |  27 +----
 web/src/api.ts                                     |  23 +----
 web/src/styles.css                                 |  11 --
 web/src/useDigests.ts                              |  69 ++-----------
 11 files changed, 251 insertions(+), 140 deletions(-)
```

**Held back:** none. **DAG smells:** none -- T0/T2 were genuinely independent and merged with
zero friction; T1's blocked-by-T0 edge was the only real dependency and it composed cleanly.

**Next:** run `/qa-plan` to review `qa/news-retention-full-load` (branch/worktree already
assembled and green).
