# solarm2m AI-news digest

A personal, daily digest of AI and .NET-engineering news. A scheduled .NET 10 job fetches
a handful of sources, ranks them against an interest profile, summarises the top ~15 with
an LLM, and stores them in Cloudflare D1. A mobile-first, installable PWA reads them back
through a Cloudflare Pages Function.

Free to run, no always-on servers, and an end-to-end .NET + AI portfolio piece.

**Live:** https://solarm2m.com

---

## How it works

```
                    GitHub Actions (cron ~05:00 UTC)
                                  │
                 ┌────────────────┴─────────────────┐
                 │  Digest.Ingest (.NET 10 console)  │
                 │  fetch → dedupe → score → rank →  │
                 │  summarise (Gemini via IChatClient)│
                 └────────────────┬─────────────────┘
                                  │  D1 HTTP REST API (parameterised, ON CONFLICT)
                                  ▼
                        ┌───────────────────┐
                        │  Cloudflare D1     │  (serverless SQLite)
                        └─────────┬──────────┘
                                  │  D1 binding (read-only path)
                                  ▼
        ┌──────────────────────────────────────────────┐
        │  Cloudflare Pages                               │
        │   • /functions/api/*  → JSON read API           │
        │   • /web (React PWA)   → static, offline-capable │
        └──────────────────────────────────────────────┘
                                  ▲
                                  │  HTTPS (apex domain)
                              your phone
```

The browser never touches D1 directly: it calls `/api/digests`, a Pages Function with a D1
binding. The Gemini key and Cloudflare token live only in GitHub Actions secrets.

### Stack

| Layer          | Choice                                                                   |
| -------------- | ------------------------------------------------------------------------ |
| Ingestion      | C# **.NET 10** console app, run on a schedule by GitHub Actions          |
| Summarisation  | **Gemini 2.5 Flash** (free tier) via its OpenAI-compatible endpoint, consumed through `Microsoft.Extensions.AI` `IChatClient` (provider-swappable) |
| Store          | **Cloudflare D1** (SQLite). Writes via the D1 HTTP REST API; schema via Wrangler migrations |
| Read API       | **Cloudflare Pages Function** with a D1 binding (`/api/digests`, `/api/digests/:date`) |
| Frontend       | **React + TypeScript + Vite**, mobile-first installable **PWA** with offline cache |
| Hosting        | **Cloudflare Pages** on the apex domain `solarm2m.com`                   |

---

## Repository layout

```
ingest/        .NET 10 console app (Digest.Ingest) + tests (Digest.Ingest.Tests)
functions/     Cloudflare Pages Functions — the JSON read API
web/           React + Vite PWA frontend
migrations/    D1 schema migrations (Wrangler)
wrangler.toml  D1 config for migrations / local dev (NOT a Pages config — see notes)
seed.dev.sql   Sample data for local frontend dev only
package.json   Root tooling: Wrangler, D1 migrations, Functions type-check
```

## Data model (D1)

`digest_item(id, date, category, title, source, url, summary, published_at, score, created_at)`
— `UNIQUE(url)` for dedupe, index on `date`. Upserts use
`INSERT ... ON CONFLICT(url) DO NOTHING`, so re-running a day never duplicates.

Categories (slugs): `dotnet-azure`, `ai-engineering`, `research`, `domain`, `local-llms`.

## Sources

RSS/Atom where possible, API/scrape otherwise. One failing source never aborts a run.

- **.NET Blog**, **Simon Willison**, **Latent Space** — RSS/Atom
- **r/LocalLLaMA** — Reddit's Atom feed (the JSON API blocks unauthenticated clients)
- **Hacker News** — Algolia API, recent stories above a points threshold
- **arXiv** cs.AI + cs.CL — Atom API, title-filtered to the interest profile
- **Anthropic** news + engineering — no feed, so article links are scraped and titles read
  from each page's `og:title`

Ranking favours: .NET/C# AI tooling, `Microsoft.Extensions.AI`, Agent Framework, Semantic
Kernel, Azure OpenAI / AI Foundry, RAG, agents, evals, document intelligence / structured
extraction, local LLMs / Ollama, and AI for accounting / fintech.

---

## Secrets

| Name              | Used by                                  | What it is                                    |
| ----------------- | ---------------------------------------- | --------------------------------------------- |
| `GEMINI_API_KEY`  | ingest (summariser)                      | Google AI Studio API key (free tier)          |
| `CF_API_TOKEN`    | ingest (D1 REST) + Wrangler migrations   | Cloudflare API token scoped to **D1 : Edit**  |
| `CF_ACCOUNT_ID`   | ingest (D1 REST) + Wrangler              | Cloudflare account id                         |
| `D1_DATABASE_ID`  | ingest (D1 REST) + Wrangler migrations   | D1 database id from `wrangler d1 create`      |

Secrets are provided as **GitHub Actions secrets** in CI and as **`dotnet user-secrets`** or
environment variables locally. They are never committed. See [`.env.example`](.env.example).

---

## Local development

### Prerequisites

- **.NET 10 SDK**, **Node 20.19+ / 22+**. Wrangler is pinned in `package.json` (no global install).

### 1. Ingest app — dry run (no secrets needed)

Fetches, ranks and prints the top items without summarising or writing anywhere:

```bash
cd ingest
dotnet run --project Digest.Ingest -- --dry-run
```

A real run additionally summarises via Gemini and writes to your **remote** D1 (there is no
local D1 REST endpoint). Provide secrets via user-secrets, then run without `--dry-run`:

```bash
cd ingest/Digest.Ingest
dotnet user-secrets set "Gemini:ApiKey"        "<GEMINI_API_KEY>"
dotnet user-secrets set "CloudflareD1:AccountId" "<CF_ACCOUNT_ID>"
dotnet user-secrets set "CloudflareD1:DatabaseId" "<D1_DATABASE_ID>"
dotnet user-secrets set "CloudflareD1:ApiToken"  "<CF_API_TOKEN>"
dotnet run --project .                          # writes to remote D1
```

Run the tests with `dotnet test ingest/Digest.Ingest.slnx`.

### 2. Frontend + read API (local D1)

From the repo root, set up a local D1 and load sample data:

```bash
npm ci                 # root tooling (Wrangler)
npm run migrate:local  # create/migrate the local D1
npm run seed:local     # load seed.dev.sql sample rows
```

Then run the two dev processes (the local D1 binding is read from `wrangler.toml`):

```bash
# terminal A — Pages Functions + local D1 on :8788
npm run dev:api

# terminal B — Vite dev server on :5173 (proxies /api → :8788)
npm --prefix web run dev
```

Open http://localhost:5173. Prefer a single process? Build once and use Wrangler only:
`npm --prefix web run build && npm run dev:api`, then open http://localhost:8788.

---

## Deploy (one-time setup)

All on Cloudflare — no third-party certificate or DNS validation steps.

1. **Create the D1 database** and note the id:
   ```bash
   npm ci
   npx wrangler login
   npm run d1:create        # → copy the database_id it prints
   ```
2. **Create an API token** (Cloudflare dashboard → My Profile → API Tokens) with permission
   **Account → D1 → Edit**. This single token serves both the .NET writer and Wrangler.
3. **Get a Gemini key** from Google AI Studio (free tier).
4. **Create the Pages project** (dashboard → Workers & Pages → Pages → connect this repo):
   - Build command: `npm --prefix web ci && npm --prefix web run build`
   - Build output directory: `web/dist`
   - Root directory: `/` (so `/functions` is detected)
5. **Bind D1 to Pages** (Pages project → Settings → Functions → D1 database bindings):
   variable name **`DB`** → database **`solarm2m_digest`**. (Production bindings live here, not
   in `wrangler.toml` — see notes.)
6. **Custom domain**: Pages → Custom domains → add `solarm2m.com`. Cloudflare wires DNS + TLS.
7. **Add GitHub Actions secrets** (repo → Settings → Secrets and variables → Actions):
   `GEMINI_API_KEY`, `CF_API_TOKEN`, `CF_ACCOUNT_ID`, `D1_DATABASE_ID`.
8. **First run**: trigger the **Daily ingest** workflow manually (Actions → Daily ingest → Run
   workflow). It applies migrations to remote D1, then ingests.

## CI/CD

- **`.github/workflows/ingest.yml`** — scheduled (~05:00 UTC) + manual. Applies D1 migrations
  (`--remote`, injecting `D1_DATABASE_ID` into `wrangler.toml` at runtime) then runs the .NET
  ingest. The Actions run log is the record.
- **`.github/workflows/ci.yml`** — on push/PR: builds & tests the .NET app, type-checks the
  Functions, and builds the web app.
- **Cloudflare Pages** auto-builds the frontend and Functions on every push to `main`.

---

## Notes & caveats

- **Provider-swappable summariser.** Gemini is reached through its OpenAI-compatible endpoint
  and consumed via `Microsoft.Extensions.AI.IChatClient`. Point `Gemini:Endpoint`/`Model` at
  another OpenAI-compatible provider (or swap the DI registration) to change models.
- **`database_id` is kept out of git.** `wrangler.toml` carries a placeholder; CI substitutes
  the real id for remote migrations, and production Pages binds D1 via the dashboard. That is
  why `wrangler.toml` is intentionally **not** a Pages config (no `pages_build_output_dir`).
- **Free-tier limits.** Summaries are issued sequentially with a delay
  (`Gemini:DelayBetweenCallsMs`) to stay within Gemini's requests-per-minute limit; ~15
  items/day is well within the daily cap.
- **Scheduled crons** can run late or be skipped under load and auto-disable after 60 days of
  repo inactivity — the daily run keeps it alive, and lateness is fine for a morning read.
- **Anthropic** has no feed, so titles are scraped from `og:title`; if the markup changes the
  source degrades gracefully (slug-derived titles) rather than failing the run.
