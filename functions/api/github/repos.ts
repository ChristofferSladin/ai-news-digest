/// <reference types="@cloudflare/workers-types" />

import { errorResponse, jsonResponse } from "../_shared";

// GET /api/github/repos
// Live view of AI repositories on GitHub, in two groups:
//   newest   — the 5 highest-starred AI repos created in the last couple of days
//   trending — repos gaining stars fastest right now (star velocity, see rankTrending)
//
// Deliberately NOT backed by D1: this data is cheap to re-query and stale rows would be
// worse than a fresh miss. The Function exists purely to (a) hide a GitHub token and
// (b) collapse every visitor onto one cached upstream call — GitHub's search API allows
// only 10 req/min unauthenticated (30 with a token), and a Worker shares its egress IP
// with everything else on the edge, so calling GitHub from the browser is not an option.

interface GithubEnv {
  /** Optional fine-grained token, no scopes needed (public data only). Raises the search
   *  rate limit from 10 to 30 req/min. Set with `wrangler pages secret put GITHUB_TOKEN`. */
  GITHUB_TOKEN?: string;
}

/** Topic qualifiers are AND-ed by GitHub, so each topic needs its own query; results are
 *  merged and globally re-ranked. Noisy topics are harmless — low-star repos never survive
 *  the merge. `OR` is not valid syntax in the repository search API. */
const TOPICS = ["ai", "llm", "machine-learning"] as const;

const NEW_COUNT = 5;
const TRENDING_COUNT = 8;

/** Two days, not one: a same-day window regularly holds fewer than five repos worth showing. */
const NEW_WINDOW_DAYS = 2;

const TRENDING_PUSHED_WITHIN_DAYS = 7;
const TRENDING_MAX_AGE_DAYS = 365;
const TRENDING_MIN_STARS = 100;
/** Candidates pulled per topic before re-ranking by velocity. */
const TRENDING_POOL = 30;

const CACHE_SECONDS = 1800;
const DAY_MS = 24 * 60 * 60 * 1000;

/** The `stargazers_count`-bearing subset of a search result we actually use. */
interface GithubRepo {
  id: number;
  full_name: string;
  html_url: string;
  description: string | null;
  stargazers_count: number;
  language: string | null;
  topics?: string[];
  created_at: string;
  pushed_at: string;
  owner: { login: string };
}

/** Client-facing shape. */
export interface Repo {
  id: number;
  name: string;
  owner: string;
  url: string;
  description: string;
  stars: number;
  language: string | null;
  topics: string[];
  createdAt: string;
  pushedAt: string;
}

function isoDaysAgo(days: number): string {
  return new Date(Date.now() - days * DAY_MS).toISOString().slice(0, 10);
}

async function search(query: string, perPage: number, token?: string): Promise<GithubRepo[]> {
  const url = new URL("https://api.github.com/search/repositories");
  url.searchParams.set("q", query);
  url.searchParams.set("sort", "stars");
  url.searchParams.set("order", "desc");
  url.searchParams.set("per_page", String(perPage));

  const headers: Record<string, string> = {
    accept: "application/vnd.github+json",
    "x-github-api-version": "2022-11-28",
    // GitHub rejects API requests that omit a User-Agent.
    "user-agent": "solarm2m-digest",
  };
  if (token) {
    headers.authorization = `Bearer ${token}`;
  }

  const response = await fetch(url.toString(), { headers });
  if (!response.ok) {
    throw new Error(`GitHub search failed (HTTP ${response.status}).`);
  }

  const body = (await response.json()) as { items?: GithubRepo[] };
  return body.items ?? [];
}

/** Runs one search per topic and merges them, keeping the first sighting of each repo.
 *  Tolerates partial failure; throws only if every topic query failed. */
async function gather(queries: string[], perPage: number, token?: string): Promise<GithubRepo[]> {
  const settled = await Promise.allSettled(queries.map((q) => search(q, perPage, token)));

  const byId = new Map<number, GithubRepo>();
  let failure: unknown;
  for (const result of settled) {
    if (result.status === "rejected") {
      failure ??= result.reason;
      continue;
    }
    for (const repo of result.value) {
      if (!byId.has(repo.id)) {
        byId.set(repo.id, repo);
      }
    }
  }

  if (byId.size === 0 && failure !== undefined) {
    throw failure instanceof Error ? failure : new Error("GitHub search failed.");
  }
  return [...byId.values()];
}

/** Stars per day since creation. GitHub has no trending endpoint, and raw star counts just
 *  rank the all-time giants — velocity is what makes "trending" mean anything here. */
function starVelocity(repo: GithubRepo): number {
  const ageDays = Math.max(1, (Date.now() - Date.parse(repo.created_at)) / DAY_MS);
  return repo.stargazers_count / ageDays;
}

function toDto(repo: GithubRepo): Repo {
  return {
    id: repo.id,
    name: repo.full_name,
    owner: repo.owner.login,
    url: repo.html_url,
    description: repo.description ?? "",
    stars: repo.stargazers_count,
    language: repo.language,
    topics: (repo.topics ?? []).slice(0, 4),
    createdAt: repo.created_at,
    pushedAt: repo.pushed_at,
  };
}

export const onRequestGet: PagesFunction<GithubEnv> = async (context) => {
  // Query params are ignored, so every visitor collapses onto one key.
  const cacheKey = new Request(new URL("/api/github/repos", context.request.url).toString());
  const cached = await caches.default.match(cacheKey);
  if (cached) {
    return cached;
  }

  const token = context.env.GITHUB_TOKEN;
  const createdSince = isoDaysAgo(NEW_WINDOW_DAYS);
  const pushedSince = isoDaysAgo(TRENDING_PUSHED_WITHIN_DAYS);
  const bornAfter = isoDaysAgo(TRENDING_MAX_AGE_DAYS);

  try {
    const [fresh, candidates] = await Promise.all([
      gather(
        TOPICS.map((topic) => `topic:${topic} created:>=${createdSince}`),
        NEW_COUNT,
        token,
      ),
      gather(
        TOPICS.map(
          (topic) =>
            `topic:${topic} pushed:>=${pushedSince} created:>=${bornAfter} ` +
            `stars:>=${TRENDING_MIN_STARS}`,
        ),
        TRENDING_POOL,
        token,
      ),
    ]);

    const newest = fresh
      .sort((a, b) => b.stargazers_count - a.stargazers_count)
      .slice(0, NEW_COUNT)
      .map(toDto);

    const trending = candidates
      .sort((a, b) => starVelocity(b) - starVelocity(a))
      .slice(0, TRENDING_COUNT)
      .map(toDto);

    const response = jsonResponse(
      { generatedAt: new Date().toISOString(), newest, trending },
      { cacheSeconds: CACHE_SECONDS },
    );
    context.waitUntil(caches.default.put(cacheKey, response.clone()));
    return response;
  } catch (err) {
    return errorResponse(err instanceof Error ? err.message : "Unexpected error.", 502);
  }
};
