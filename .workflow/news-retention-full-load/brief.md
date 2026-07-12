# Brief: Load all digest items + 30-day retention purge (repo: solarm2m-digest, base: main)

## What the human asked (raw)
"when opening the live page (production) a handful of news are loaded (cca 60 news). and then
when clicking between categories, more are spawned from the history. i want to A: load all of
them. B: delete news that are timestamped 30 days back in time. so the database does not get
cluttered."

## Current state vs desired (the diff)
- Now: `GET /api/digests?days=N&before=D` pages by distinct day count (default 7, max 31,
  `before` cursor for backward paging). `useDigests.ts` loads 7 days on mount (~60 items, since
  not every day hits the 15-item/day cap), then an `IntersectionObserver` sentinel at the page
  bottom (`App.tsx:34-49`) triggers `loadMore()` for infinite scroll. `CategoryChips` filtering
  is 100% client-side (`App.tsx:25-32`) — it never fetches. But filtering shrinks the visible
  list, which can bring the sentinel into view without real scrolling, silently firing
  `loadMore()` and merging in another 7 days of history. That's the "spawns more news when
  clicking categories" bug — a side effect of infinite scroll, not categories fetching.
  No retention/deletion logic exists anywhere (ingest, migrations, or a scheduled job);
  `digest_item` grows forever.
- Want: frontend fetches everything in a single request on load, no more sentinel/loadMore/
  hasMore/before-cursor machinery. The API's `days` param becomes optional — omitted means
  uncapped (return every row), decoupling the frontend from the retention window. The nightly
  ingest run (`IngestRunner`), after existing scoring/selection, purges `digest_item` rows
  older than a rolling 30-day window keyed on the `date` column (digest day) — running
  unconditionally (even when a day yields 0 new items), but never in dry-run mode.

## Acceptance criteria (testable)
- [ ] `GET /api/digests` with no `days` param returns all rows across all distinct dates present
      in D1 (no 7- or 31-day cap) -- verified by: seed >31 distinct dates locally, `curl
      localhost:8788/api/digests` returns items for every date.
- [ ] `GET /api/digests?days=N` still supports an explicit cap -- verified by: `curl
      'localhost:8788/api/digests?days=2'` returns exactly the 2 newest dates.
- [ ] The API no longer accepts/uses a `before` cursor param -- verified by: reading
      `functions/api/digests.ts`, no cursor logic remains.
- [ ] Frontend loads once on mount and issues no further automatic fetches -- verified by:
      browser network tab shows exactly one `/api/digests` call regardless of scrolling.
- [ ] Clicking category chips never triggers a network request -- verified by: browser network
      tab, click through every chip.
- [ ] Sentinel/`IntersectionObserver`/`loadMore`/`hasMore` paging removed from `useDigests.ts`
      and `App.tsx` -- verified by: grep for those terms in `web/src` returns nothing.
- [ ] A nightly ingest run deletes `digest_item` rows with `date` older than (that run's digest
      date minus 30 days) -- verified by: new `Digest.Ingest.Tests` case asserting a DELETE
      statement with the correct cutoff is issued.
- [ ] Purge runs even when `kept.Count == 0` for the day -- verified by: test covering the
      zero-kept path still invokes the purge.
- [ ] Purge does NOT run in dry-run mode -- verified by: existing dry-run test path shows no
      delete/repository call.
- [ ] `npm run typecheck:functions` and `web`'s `npm run build` (`tsc --noEmit`) pass.
- [ ] `dotnet test` passes in `ingest/Digest.Ingest.Tests`, including new purge tests.

## Non-goals / out of scope
- `GET /api/digests/:date` (single-day endpoint) is untouched.
- No separate scheduled Cloudflare job/cron for purge -- reuses the existing daily ingest run.
- No change to the ~15-items/day selection cap, scoring, or category logic.
- No pull-to-refresh or manual "load more" UI -- infinite scroll is removed, not replaced.
- No one-time manual backfill/cleanup of the current production D1 -- the purge is a single
  `date < cutoff` DELETE, so the first nightly run after this ships purges all existing
  backlog older than 30 days in one shot.

## Blast radius
- Fair game: `functions/api/digests.ts`, `functions/api/_shared.ts`, `web/src/useDigests.ts`,
  `web/src/App.tsx`, `web/src/api.ts`, `ingest/Digest.Ingest/Pipeline/IngestRunner.cs`,
  `ingest/Digest.Ingest/Storage/D1DigestRepository.cs`, `ingest/Digest.Ingest/Storage/
  IDigestRepository.cs`, `ingest/Digest.Ingest.Tests/*`.
- Off limits: `functions/api/digests/[date].ts`, `migrations/` (no schema change -- `date`
  column + index already exist), `.github/workflows/*` (no new cron needed),
  `Categorizer`/`RelevanceScorer`/`DigestSelector` (selection logic untouched).

## Vertical slice (core flow, top to bottom)
DB (`digest_item`, existing schema, no migration)
  -> `IngestRunner.RunAsync`: after existing upsert (and on the early-return-with-no-kept-items
     branch), call a new repository method with a cutoff date = digestDate - 30 days
  -> `IDigestRepository` / `D1DigestRepository`: new method issuing
     `DELETE FROM digest_item WHERE date < ?`
  -> (read side, independent) `functions/api/digests.ts`: `days` optional, uncapped when
     omitted; `before` removed
  -> `web/src/api.ts`: `fetchDigests()` called with no params
  -> `web/src/useDigests.ts`: single fetch on mount, no `loadMore`/`hasMore`
  -> `web/src/App.tsx`: drop sentinel ref/`IntersectionObserver` effect; category chips keep
     working exactly as now (client-side filter over the full loaded set)

## Constraints
- Free-tier Cloudflare D1 / GitHub Actions -- no new paid infra, no new secrets.
- `ingest.yml` already prevents overlapping runs (`concurrency: group: ingest`), so purge
  running in the same process as upsert is safe.
- Dry run must remain a no-write path (existing contract: "no model calls, no writes").
- Cache headers on `/api/digests` (900s / max-age 300 capped) unaffected.

## Edge cases & failure modes
- Ingest run with 0 qualifying items for the day -> purge still runs, anchored on that day's
  digest date.
- First deploy after this change: current prod D1 may already have a backlog older than 30
  days. No separate migration needed -- the next nightly run's DELETE purges it all in one
  statement, not incrementally.
- Empty DB / brand-new date with no prior rows -> DELETE affects 0 rows, no error.
- `/api/digests` with no `days` and no data at all -> returns `{ days: [] }`, unchanged.
- `seed.dev.sql` dates are fixed (2026-06-01/05-31) and will look "old" relative to real
  current date, but the purge only runs from the .NET ingest pipeline (never from the
  `seed:local` wrangler script), so local frontend dev is unaffected.

## Decisions log (resolved during grilling)
- Q: How does "spawn from history on category click" actually happen? -> A: Confirmed --
  sentinel-triggered `loadMore()` firing when category-filtering shrinks page height, not a
  direct category-to-fetch call.
- Q: Keep pagination infra and patch the trigger, or remove pagination entirely? -> A: Remove
  entirely; single full-load fetch (Recommended option chosen).
- Q: Retention key -- `date` vs `published_at` vs `created_at`? -> A: `date` (digest day).
- Q: Where does purge run -- inside nightly ingest, or a separate Cloudflare-side job? -> A:
  Inside nightly ingest (`IngestRunner`), reusing the existing D1 write path.
- Q: Load-all contract -- frontend requests `days=31`, or API treats omitted `days` as
  uncapped? -> A: API: omitted `days` = uncapped.
- Q: Purge on a day with 0 kept items? -> A: Purge runs unconditionally, not gated on new
  items existing.
- Q: Remove the `before` param from the API, or leave it dead? -> A: Remove entirely.
- Cutoff precision: "30 days back" = keep rows where `date >= (digestDate - 30 days)`; delete
  `date <` that boundary (a rolling 30-calendar-day window anchored on the current ingest
  run's date).

## Contradictions found vs the code
- The task's framing ("clicking categories spawns more news") doesn't match the actual code
  path (categories never fetch) -- but it's fully explained by the sentinel/infinite-scroll
  side effect, so no further design contradiction beyond what's already decided above.
