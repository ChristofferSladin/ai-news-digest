// Client for the live GitHub repo feed (/functions/api/github/repos.ts).
// Unlike the digest, nothing here is persisted — every load is upstream data.

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

export interface RepoFeed {
  generatedAt: string;
  newest: Repo[];
  trending: Repo[];
}

export async function fetchRepos(): Promise<RepoFeed> {
  const response = await fetch("/api/github/repos");
  if (!response.ok) {
    const detail = (await response.json().catch(() => null)) as { error?: string } | null;
    throw new Error(detail?.error ?? `Failed to load repos (HTTP ${response.status})`);
  }

  const data = (await response.json()) as Partial<RepoFeed>;
  return {
    generatedAt: data.generatedAt ?? new Date().toISOString(),
    newest: data.newest ?? [],
    trending: data.trending ?? [],
  };
}
