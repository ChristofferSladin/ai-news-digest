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

/** Fetches every digest day in the store (the API returns uncapped history when `days` is omitted). */
export async function fetchDigests(signal?: AbortSignal): Promise<DigestDay[]> {
  const response = await fetch("/api/digests", { signal });
  if (!response.ok) {
    throw new Error(`Failed to load digests (HTTP ${response.status})`);
  }

  const data = (await response.json()) as DigestsResponse;
  return data.days ?? [];
}
