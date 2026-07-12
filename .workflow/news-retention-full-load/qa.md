# QA: news-retention-full-load   (night of 2026-07-12, branch qa/news-retention-full-load off main@b59030f)

## Decisions the night made  (bless or redirect)
- **T0 Read API uncapped history window** [done] -- decided: garbage/non-finite `days` values
  clamp to `MAX_DAYS` (31) rather than being treated as uncapped; uncapped only when the param
  is entirely absent. Because malformed input degrading to "everything" felt like the wrong
  failure mode for a public query param.
  [x] OK  [ ] Change: __
- **T2 Ingest 30-day retention purge** [done] -- decided: retention window is a plain
  `private const int RetentionDays = 30` on `IngestRunner`, not an `IOptions` knob; purge lives
  directly on the existing `IDigestRepository` (no new interface); cutoff computed via
  `now.AddDays(-30)` off the injected `TimeProvider`. Because nothing else needs the window
  tunable and the ticket explicitly said not to invent config surface.
  [x] OK  [ ] Change: __
- **T1 Frontend single load, drop infinite scroll** [flagged, hitl] -- decided: dropped
  `FetchDigestsParams` entirely rather than keeping a vestigial params type; kept one `busyRef`
  re-entrancy guard in `useDigests` (unrelated to paging, just stops overlapping reloads); also
  removed now-orphaned `.sentinel`/`.footnote` CSS rules outside the ticket's listed files,
  since they existed only to style the deleted elements. Flagged because the ticket's own
  acceptance-check (a TypeScript build) can't prove runtime network behavior -- **I ran that
  verification manually myself during this QA pass** (see T1's card below) rather than leaving
  it purely as an unchecked box.
  [x] OK  [ ] Change: __

## Review  (newest first)

### T1 -- Frontend: single load, drop infinite scroll   [hitl]   [check green]
- **Claims:** Frontend fetches the full digest history once on mount and never fetches again;
  all infinite-scroll machinery (sentinel, `IntersectionObserver`, `loadMore`, `hasMore`,
  `before` cursor) removed. Category-chip filtering is untouched (still 100% client-side).
- **Diff:** `web/src/api.ts`, `web/src/useDigests.ts`, `web/src/App.tsx`, `web/src/styles.css`
  -- net -100 lines. `fetchDigests()` now takes no params; `useDigests` drops to a single
  `loading | ready | error` fetch; `App.tsx` loses the sentinel ref/effect and paging
  footnotes. (commit `c88c139`)
- **Acceptance:** `npm --prefix web run build` -> **PASS** (`tsc --noEmit && vite build` clean,
  PWA precache generated normally).
- **Decisions:** see above.
- **Manual QA -- actually performed, not just described:** seeded local D1 with items across
  12 distinct dates (2026-06-20 through 2026-07-11, spanning well past the old 7-day default),
  built `web/dist`, booted `wrangler pages dev` on the assembled `qa/news-retention-full-load`
  branch, and drove it with the browser tool:
  - Fresh load: **exactly one** `GET /api/digests` request (no query string), all 12 days
    rendered on the page in one shot.
  - Clicked "AI engineering", "Local LLMs", and "All" chips in turn: page content filtered
    correctly each time (verified via rendered text -- e.g. selecting "Local LLMs" showed only
    "Sample local N" items across all 8 of its dates), and **zero additional network requests**
    fired on any click (network log stayed at the same 2 entries -- the initial `/api/digests`
    call plus one unrelated static PWA asset -- throughout every click).
  - No console errors during any of this.
  - Not separately re-verified: the Retry-button double-click de-dupe path (low risk, and the
    `busyRef` guard covering it is a straightforward re-entrancy check).
- [x] Approve   [ ] Request changes: ____

### T2 -- Ingest: 30-day retention purge   [afk]   [check green]
- **Claims:** Every nightly ingest run deletes `digest_item` rows whose `date` is older than a
  rolling 30-day window anchored on that run's digest date -- unconditionally, except never
  during dry-run.
- **Diff:** `IDigestRepository.cs` (+7), `D1DigestRepository.cs` (+9, new
  `PurgeOlderThanAsync` issuing `DELETE FROM digest_item WHERE date < ?`), `IngestRunner.cs`
  (+20, `RetentionDays = 30` const + call sites on both the zero-kept-items early-return path
  and the normal path), plus new/extended tests in `Digest.Ingest.Tests` (+177 across three
  files, including a new `IngestRunnerTests.cs`). (commit `ff61ad0`)
- **Acceptance:** `dotnet test ingest/Digest.Ingest.Tests --filter "FullyQualifiedName~Purge"`
  -> **PASS**, 6/6. Full suite `dotnet test ingest/Digest.Ingest.Tests` -> **PASS**, 66/66, 0
  regressions.
- **Decisions:** see above.
- **Manual QA:** read the diff directly (see this QA pass's earlier code review) -- purge call
  sites are correctly placed relative to the `dryRun` short-circuit and the zero-kept early
  return; cutoff arithmetic reuses the injected `TimeProvider` (no `DateTimeOffset.UtcNow`
  smell); SQL stays parameterized.
- [x] Approve   [ ] Request changes: ____

### T0 -- Read API: uncapped history window   [afk]   [check green]
- **Claims:** `GET /api/digests` with no `days` param returns every distinct digest day in the
  store; an explicit `days=N` still caps to the N most recent days (clamped 1..31); the
  backward-paging `before` cursor is removed entirely.
- **Diff:** `functions/api/digests.ts` only -- 23 insertions, 25 deletions, net simpler
  (single `parseDays` helper, ternary SQL branch for capped-vs-uncapped). (commit `708b9bd`)
- **Acceptance:** scripted boot-and-probe -- seeded 35 distinct dates locally, booted
  `wrangler pages dev`, `curl` with no params returned all 35, `?days=3` returned exactly 3 ->
  **PASS**.
- **Decisions:** see above.
- **Manual QA:** re-verified live during this QA pass's T1 walkthrough -- the same running
  instance served all 12 seeded dates with zero query string, confirming T0's contract holds
  on the assembled branch, not just in T0's own isolated worktree.
- [x] Approve   [ ] Request changes: ____

## Integration
- Full suite on `qa/news-retention-full-load`: **green**. `dotnet test
  ingest/Digest.Ingest.Tests` 66/66; `npm run typecheck:functions` clean; `npm --prefix web run
  build` clean. Additionally, a full-stack boot-and-probe (local D1 seeded across 12 distinct
  dates, `wrangler pages dev` serving both the API and the built frontend together) confirmed
  the composed read-path vertical (T0 + T1) end-to-end, and a live browser walkthrough
  confirmed the frontend's one-fetch-then-client-side-filter behavior with zero surprise
  network activity -- this is the actual behavior the original bug report was about.
- Held back from merge: **none**. All three ticket branches (`ticket/T0`, `ticket/T1`,
  `ticket/T2`) merged into `qa/news-retention-full-load` with zero conflicts -- T0/T2 touched
  fully disjoint files (frontend+Functions vs. .NET ingest), and T1 (branched from T0) only
  touched `web/src/*`, disjoint from T2's `ingest/*`.
- Consolidated diff vs. base (`main@b59030f`): 11 files changed, 251 insertions(+),
  140 deletions(-) -- net code *reduction* on the frontend/API side (dead paging code removed)
  plus a small, well-tested addition on the ingest side (the purge).

## After approval
Merge `qa/news-retention-full-load` -> `main`; then strip the scaffolding --
`git rm -r .workflow/news-retention-full-load/` and commit. This repo has no `PRODUCT.md` or
similar planning doc to reconcile into (checked -- only `README.md` exists at the root), so no
further sync step applies.
