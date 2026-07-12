## Board: news-retention-full-load   (base: main)

| id | title                                   | type | status | blocked-by |
|----|------------------------------------------|------|--------|------------|
| T0 | Read API: uncapped history window       | afk  | todo   | -          |
| T1 | Frontend: single load, drop infinite scroll | hitl | todo   | T0         |
| T2 | Ingest: 30-day retention purge           | afk  | todo   | -          |

Ready-set at start: T0, T2 (independent runtimes/spines, no shared seam). T1 becomes ready once
T0 is terminal (done or flagged).

---

### T0 -- Read API: uncapped history window
- **type:** afk
- **status:** todo
- **blocked-by:** -
- **module:** Digest history window (read API) -- interface: one optional parameter selecting
  either "the N most recent digest days" (explicit) or "all of them" (omitted); hides: the
  day-bucketing SQL and how rows group into day buckets for the response.
- **slice:** `digest_item` (D1, unchanged schema) -> read API's day-window query -> JSON
  response shape (unchanged). Also removes the backward-paging cursor parameter entirely.
- **acceptance-check:**
  ```
  npm run typecheck:functions && npm run migrate:local >/dev/null && \
  for i in $(seq 1 35); do d=$(date -v-${i}d +%F); \
    npx wrangler d1 execute solarm2m_digest --local --command \
    "INSERT INTO digest_item (date,category,title,source,url,summary,score,created_at) VALUES ('$d','research','T$i','Test','https://example.com/t$i','s',1.0,'$d') ON CONFLICT(url) DO NOTHING;" >/dev/null; \
  done && \
  (npm run dev:api & echo $! > /tmp/digest_api.pid) && sleep 3 && \
  ALL=$(curl -s localhost:8788/api/digests | jq '.days | length') && \
  CAPPED=$(curl -s "localhost:8788/api/digests?days=3" | jq '.days | length') && \
  kill "$(cat /tmp/digest_api.pid)" 2>/dev/null; \
  test "$ALL" -ge 35 && test "$CAPPED" -eq 3 && echo PASS
  ```
  Expect `PASS`: omitting `days` returns every seeded date (>= 35), an explicit `days=3` still
  returns exactly 3.
- **files-likely-touched:** functions/api/digests.ts, functions/api/_shared.ts
- **decisions:** (empty -- night agent appends judgement calls here)
- **notes:** Per brief.md decisions log: omitted `days` = uncapped (not "frontend requests
  days=31"). Remove the `before` cursor parameter and its query branch entirely -- nothing
  calls it once T1 lands. `functions/api/digests/[date].ts` (single-day endpoint) is off
  limits -- untouched. See prd.md "Digest history window" module and its acceptance criteria.

---

### T1 -- Frontend: single load, drop infinite scroll
- **type:** hitl
- **status:** todo
- **blocked-by:** T0
- **module:** Digest loading (frontend) -- interface: a single hook exposing the currently
  loaded days plus a simple status (loading/ready/error), no paging controls in its public
  surface; hides: the one-shot fetch lifecycle.
- **slice:** page mount -> one fetch with no history-window param (relies on T0's "omitted =
  uncapped") -> hook state -> page renders every loaded day -> category chips filter
  client-side over the full loaded set (unchanged behavior, must keep working).
- **acceptance-check:**
  ```
  npm --prefix web run build
  ```
  Expect a clean build (tsc --noEmit + vite build) with no references to the removed
  sentinel/IntersectionObserver/loadMore/hasMore/before-cursor concepts. This proves the code
  compiles and the dead paging code is gone; it does NOT prove runtime network behavior (no
  E2E harness exists in this repo) -- **flag for manual QA:** open the live/local page, confirm
  exactly one `/api/digests` request fires on load and clicking every category chip causes zero
  further requests.
- **files-likely-touched:** web/src/useDigests.ts, web/src/api.ts, web/src/App.tsx
- **decisions:** (empty -- night agent appends judgement calls here)
- **notes:** Remove: the sentinel ref + IntersectionObserver effect in App.tsx, loadMore/
  hasMore/busyRef/hasMoreRef/mergeDays in useDigests.ts, the `before` param plumbing in api.ts.
  Keep: CategoryChips and the client-side `visibleDays` filter in App.tsx exactly as-is --
  category filtering is a separate, already-correct deep module (see prd.md). Tagged hitl
  because there's no automated way to assert "zero extra network calls" in this repo today;
  QA confirms by hand per qa-plan.

---

### T2 -- Ingest: 30-day retention purge
- **type:** afk
- **status:** todo
- **blocked-by:** -
- **module:** Digest retention policy (ingest) -- interface: one operation, "purge everything
  older than a given cutoff date," invoked once per ingest run; hides: cutoff-date arithmetic,
  the storage-layer delete statement, and the decision of when it fires.
- **slice:** IngestRunner.RunAsync (after existing scoring/selection, including the
  zero-kept-items early-return path) -> new repository method -> D1 DELETE keyed on the
  `date` column -- a full top-to-bottom slice within the ingest console app.
- **acceptance-check:**
  ```
  dotnet test ingest/Digest.Ingest.Tests --filter "FullyQualifiedName~Purge"
  ```
  Expect new, green tests covering: (a) a DELETE is issued with cutoff = run date - 30 days,
  (b) the purge still runs when `kept.Count == 0`, (c) the purge does NOT run when `dryRun` is
  true. Also run `dotnet test ingest/Digest.Ingest.Tests` (full suite) to confirm no regression.
- **files-likely-touched:** ingest/Digest.Ingest/Pipeline/IngestRunner.cs,
  ingest/Digest.Ingest/Storage/D1DigestRepository.cs,
  ingest/Digest.Ingest/Storage/IDigestRepository.cs, ingest/Digest.Ingest.Tests/*
- **decisions:** (empty -- night agent appends judgement calls here)
- **notes:** Per brief.md decisions log: retention keys off `date` (digest day), not
  `published_at`/`created_at`. Cutoff = current run's digest date minus 30 days; delete rows
  with `date` strictly less than that boundary. Purge is unconditional except for dry-run (no
  writes at all in dry-run is an existing contract -- do not break it). No schema migration
  needed -- `date` is already indexed. Follow the existing `FakeD1Client`/test-double pattern
  in `ingest/Digest.Ingest.Tests/TestDoubles.cs` and `D1DigestRepositoryTests.cs` for the new
  tests. Fully independent of T0/T1 -- different runtime (.NET console app vs. Pages
  Functions/React), no shared files, safe to run in parallel from the start.
