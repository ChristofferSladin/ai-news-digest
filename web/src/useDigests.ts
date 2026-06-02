import { useCallback, useEffect, useRef, useState } from "react";
import { type DigestDay, fetchDigests } from "./api";

const PAGE_DAYS = 7;

export type LoadStatus = "loading" | "ready" | "loading-more" | "error";

export interface UseDigests {
  days: DigestDay[];
  status: LoadStatus;
  error: string | null;
  hasMore: boolean;
  loadMore: () => void;
  reload: () => void;
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : "Something went wrong.";
}

/** Merges older days after the current ones, guarding against duplicate dates. */
function mergeDays(current: DigestDay[], older: DigestDay[]): DigestDay[] {
  const seen = new Set(current.map((d) => d.date));
  return [...current, ...older.filter((d) => !seen.has(d.date))];
}

export function useDigests(): UseDigests {
  const [days, setDays] = useState<DigestDay[]>([]);
  const [status, setStatus] = useState<LoadStatus>("loading");
  const [error, setError] = useState<string | null>(null);
  const [hasMore, setHasMore] = useState(true);

  // Refs mirror state so the observer callback stays stable and closure-safe.
  const busyRef = useRef(false);
  const daysRef = useRef<DigestDay[]>([]);
  const hasMoreRef = useRef(true);

  const apply = useCallback((next: DigestDay[]) => {
    daysRef.current = next;
    setDays(next);
  }, []);

  const setHasMoreBoth = useCallback((value: boolean) => {
    hasMoreRef.current = value;
    setHasMore(value);
  }, []);

  const reload = useCallback(async () => {
    busyRef.current = true;
    setStatus("loading");
    setError(null);
    try {
      const result = await fetchDigests({ days: PAGE_DAYS });
      apply(result);
      setHasMoreBoth(result.length >= PAGE_DAYS);
      setStatus("ready");
    } catch (err) {
      setError(messageOf(err));
      setStatus("error");
    } finally {
      busyRef.current = false;
    }
  }, [apply, setHasMoreBoth]);

  const loadMore = useCallback(async () => {
    if (busyRef.current || !hasMoreRef.current) {
      return;
    }
    const oldest = daysRef.current.at(-1)?.date;
    if (!oldest) {
      return;
    }

    busyRef.current = true;
    setStatus("loading-more");
    try {
      const older = await fetchDigests({ days: PAGE_DAYS, before: oldest });
      if (older.length === 0) {
        setHasMoreBoth(false);
      } else {
        apply(mergeDays(daysRef.current, older));
        setHasMoreBoth(older.length >= PAGE_DAYS);
      }
      setStatus("ready");
    } catch (err) {
      setError(messageOf(err));
      setStatus("error");
    } finally {
      busyRef.current = false;
    }
  }, [apply, setHasMoreBoth]);

  useEffect(() => {
    void reload();
  }, [reload]);

  // loadMore/reload are stable (useCallback); returning Promise-returning fns as () => void is fine.
  return { days, status, error, hasMore, loadMore, reload };
}
