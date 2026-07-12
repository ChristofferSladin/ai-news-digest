# PRD: Load all digest items + 30-day retention purge

## Problem statement
On the live site, only the most recent ~7 digest days (~60 items) load initially. An
infinite-scroll sentinel silently fetches more history whenever the visible list shrinks —
which happens every time a category filter is applied — so switching categories appears to
"spawn" extra news from history with no user-initiated scrolling. Separately, nothing ever
deletes old digest rows, so the database grows without bound forever.

## Solution
Load the entire digest history in a single request on page load, and remove the infinite-scroll
machinery that caused the surprise fetches. Keep the dataset itself small and bounded by having
the nightly ingest run purge digest rows older than a rolling 30-day window as part of its
existing daily write. The two halves compose: because retention keeps total history small
(≤ ~15 items/day × 30 days), "load everything" is cheap and simple rather than needing real
pagination.

## User stories
1. As a reader opening the live page, I want every available digest day loaded up front, so
   that clicking between category filters never triggers a surprise network fetch or changes
   what I'm looking at mid-read.
2. As a reader filtering by category, I want the filter to be instant and purely visual, so
   that the item counts and content don't shift under me.
3. As the site owner, I want digest rows older than 30 days purged automatically every night,
   so that the database never grows unbounded and I never have to run manual cleanup.
4. As the site owner, I want the purge to run every night regardless of whether that day
   produced new items, so that a quiet news day doesn't also mean a skipped cleanup.
5. As the site owner, I want dry-run ingest to remain side-effect-free (no writes, no deletes),
   so that I can still preview a run safely without touching production data.
6. As a future maintainer, I want the read API's history window to be a documented, optional
   parameter (not an implicit assumption baked into the frontend), so that the retention window
   can change later without a coordinated frontend change.

## Acceptance criteria (definition of done)
- [ ] Requesting the digest feed with no explicit history-window parameter returns every
      distinct digest day present in the store (no default/implicit cap) -- verified by:
      seeding >31 distinct days locally and confirming all of them come back in one response.
- [ ] The digest feed still supports an explicit, bounded history-window request (existing
      capped behavior preserved when a caller asks for it) -- verified by: requesting a small
      explicit window and confirming exactly that many of the newest days come back.
- [ ] The backward-paging cursor concept is fully removed from the read API -- verified by:
      no cursor parameter is read or referenced anywhere in the read-API source.
- [ ] The frontend issues exactly one digest fetch per page load, with no further automatic
      fetches triggered by scrolling or filtering -- verified by: opening the live page, watching
      network activity while scrolling to the bottom and clicking through every category chip.
- [ ] Category filtering remains purely client-side over already-loaded data and never changes
      network activity -- verified by: same manual check as above.
- [ ] All infinite-scroll UI/state (sentinel element, intersection-based trigger, "load more"
      affordance, "has more" state) is removed from the frontend -- verified by: reading the
      frontend digest-loading code and the page component; no such concepts remain.
- [ ] Every nightly ingest run deletes digest rows whose digest date is older than a rolling
      30-day window anchored on that run's date -- verified by: an automated test that runs the
      ingest pipeline (or the storage layer directly) with a known "today" and asserts a
      delete-older-than-cutoff operation is issued with the correct boundary date.
- [ ] The purge runs even on a day where no new items qualify for the digest -- verified by: an
      automated test covering the zero-new-items path still shows the purge being invoked.
- [ ] The purge does NOT run during a dry-run ingest -- verified by: an automated test asserting
      no delete (and no other write) occurs when dry-run is requested.
- [ ] All existing automated checks continue to pass after these changes (frontend build/
      typecheck, ingest unit tests) -- verified by: running each project's existing check command.

## Deep-module map
- **Digest retention policy** (new, ingest-side) -- interface: one operation, "purge everything
  older than a given cutoff date," invoked once per ingest run; hides: the cutoff-date
  arithmetic (today minus the retention window), the storage-layer delete statement, and the
  decision of *when* in the run it fires (unconditionally, except never during dry-run).
  Tested: yes -- unit-testable against a fake/in-memory storage double, following the existing
  pattern used for the write-side repository tests.
- **Digest history window (read API)** -- interface: one optional parameter selecting either
  "the N most recent digest days" or "all of them" when omitted; hides: the underlying
  day-bucketing query and how rows are grouped into day buckets for the response. Tested: no
  automated test harness exists for this layer today; verified by manual request + the
  project's existing type-check command.
- **Digest loading (frontend)** -- interface: a single hook/module exposing "the currently
  loaded days" plus a simple status (loading/ready/error) -- no paging controls in its public
  surface; hides: the one-shot fetch lifecycle and error handling. Tested: no frontend test
  framework exists today; verified by the project's build/typecheck command plus manual
  browser verification.
- **Category filtering (frontend, untouched)** -- already a deep module: a pure view-level
  filter over whatever is currently loaded, with no knowledge of fetching. Explicitly not
  modified -- called out so this feature doesn't accidentally re-couple filtering to network
  activity.

## Data model / schema changes
None. The existing digest-item schema already has the fields this feature needs (a digest-date
column, already indexed, that both the read API and the new retention purge key off of). No
migration required. Backward-compatible: existing rows need no transformation; the first purge
after this ships will simply delete whatever is already past the retention window in one pass.

## Vertical slices (preview of tickets)
- **Load-all (read path):** history-window parameter becomes optional/uncapped on the read
  API, backward-paging cursor removed, frontend fetches once and drops all infinite-scroll
  state/UI. Fully independent of the retention slice -- touches only the read API and frontend.
- **Retention purge (write path):** nightly ingest gains an unconditional (non-dry-run) purge
  of digest rows outside the 30-day window, with unit test coverage of the cutoff boundary,
  the zero-new-items path, and the dry-run exemption. Fully independent of the read-path slice
  -- touches only the ingest pipeline and its storage layer.
- These two slices share no code seam (different runtimes: frontend/Pages Functions vs. the
  .NET ingest console app) and can be built and verified in either order or in parallel.

## Constraints & standing rules
- No new infrastructure: no new scheduled job, no new secrets, no new paid service. The purge
  reuses the existing nightly ingest run and its existing database write credentials.
- Free-tier Cloudflare D1 -- the retention window keeps total row count small (bounded by
  roughly the daily item cap times 30), so "load everything" stays cheap without needing real
  pagination.
- Dry-run ingest must remain fully side-effect-free -- this is an existing contract, not a new
  one, and the purge must respect it.
- Existing overlap protection (only one ingest run at a time) means the purge running inside
  the same run as the daily write is safe without extra locking.
- Response caching behavior on the read API is unrelated to this change and must not regress.

## Out of scope (non-goals)
- The single-day digest endpoint is untouched.
- No manual/one-off cleanup of the current production backlog -- the next scheduled ingest run
  performs it naturally as part of its normal purge.
- No change to how many items are selected per day, how they're scored, ranked, or categorized.
- No replacement UI for infinite scroll (no "load more" button, no pull-to-refresh) -- it is
  removed, not substituted.
- No change to the retention window's trigger mechanism beyond the nightly ingest run itself.

## Risks & open questions
- Total digest volume is currently small enough that loading "everything" in one response is
  safe; if the daily item cap or retention window is raised substantially later, this should be
  revisited (no action needed now).
- None of the acceptance criteria were left unresolved coming out of grilling -- all decisions
  (retention key, purge trigger location, purge-on-zero-items behavior, cursor removal) are
  already settled in `brief.md`'s decisions log.

## Blast radius
- Fair game: the read API's digest-listing endpoint and its shared helpers; the frontend's
  digest-loading hook, the API client it calls, and the top-level page component that wires up
  category filtering and the (to-be-removed) scroll sentinel; the ingest pipeline's run
  orchestration and its storage/repository layer; the ingest test suite.
- Off limits: the single-day digest endpoint; database migrations (no schema change needed);
  CI/CD workflow definitions (no new schedule needed); relevance scoring, categorization, and
  digest-selection logic.
