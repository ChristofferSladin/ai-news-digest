import { useCallback, useEffect, useRef, useState } from "react";
import { type RepoFeed, fetchRepos } from "./github";
import type { LoadStatus } from "./useDigests";

export interface UseRepos {
  feed: RepoFeed | null;
  status: LoadStatus;
  error: string | null;
  reload: () => void;
}

/** Loads the GitHub feed the first time `enabled` goes true — i.e. when the user actually
 *  opens the Repos tab. Keeps the upstream rate limit spent only on people who look. */
export function useRepos(enabled: boolean): UseRepos {
  const [feed, setFeed] = useState<RepoFeed | null>(null);
  const [status, setStatus] = useState<LoadStatus>("loading");
  const [error, setError] = useState<string | null>(null);

  const busyRef = useRef(false);

  const reload = useCallback(async () => {
    if (busyRef.current) {
      return;
    }
    busyRef.current = true;
    setStatus("loading");
    setError(null);
    try {
      setFeed(await fetchRepos());
      setStatus("ready");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
      setStatus("error");
    } finally {
      busyRef.current = false;
    }
  }, []);

  const startedRef = useRef(false);
  useEffect(() => {
    if (enabled && !startedRef.current) {
      startedRef.current = true;
      void reload();
    }
  }, [enabled, reload]);

  return { feed, status, error, reload };
}
