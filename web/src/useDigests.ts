import { useCallback, useEffect, useRef, useState } from "react";
import { type DigestDay, fetchDigests } from "./api";

export type LoadStatus = "loading" | "ready" | "error";

export interface UseDigests {
  days: DigestDay[];
  status: LoadStatus;
  error: string | null;
  reload: () => void;
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : "Something went wrong.";
}

/** Loads every digest day once on mount. No paging: T0's read API returns full history. */
export function useDigests(): UseDigests {
  const [days, setDays] = useState<DigestDay[]>([]);
  const [status, setStatus] = useState<LoadStatus>("loading");
  const [error, setError] = useState<string | null>(null);

  // Guards against overlapping fetches (e.g. a fast double-click on Retry).
  const busyRef = useRef(false);

  const reload = useCallback(async () => {
    if (busyRef.current) {
      return;
    }
    busyRef.current = true;
    setStatus("loading");
    setError(null);
    try {
      const result = await fetchDigests();
      setDays(result);
      setStatus("ready");
    } catch (err) {
      setError(messageOf(err));
      setStatus("error");
    } finally {
      busyRef.current = false;
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { days, status, error, reload };
}
