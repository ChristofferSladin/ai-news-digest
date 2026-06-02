// Client for the read API served by Cloudflare Pages Functions (/functions/api/*).

export interface DigestItem {
  id: number;
  date: string;
  category: string;
  title: string;
  source: string;
  url: string;
  summary: string;
  publishedAt: string | null;
  score: number;
}

export interface DigestDay {
  date: string;
  items: DigestItem[];
}

interface DigestsResponse {
  days: DigestDay[];
}

export interface FetchDigestsParams {
  days?: number;
  /** Exclusive upper-bound date (yyyy-MM-dd) for paging backwards. */
  before?: string;
}

export async function fetchDigests(
  params: FetchDigestsParams = {},
  signal?: AbortSignal,
): Promise<DigestDay[]> {
  const search = new URLSearchParams();
  if (params.days) {
    search.set("days", String(params.days));
  }
  if (params.before) {
    search.set("before", params.before);
  }

  const query = search.toString();
  const response = await fetch(`/api/digests${query ? `?${query}` : ""}`, { signal });
  if (!response.ok) {
    throw new Error(`Failed to load digests (HTTP ${response.status})`);
  }

  const data = (await response.json()) as DigestsResponse;
  return data.days ?? [];
}
