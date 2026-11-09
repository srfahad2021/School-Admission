import { useCallback, useEffect, useRef, useState } from "react";
import { errorMessage, serverNow } from "./api";

// Calls fn immediately and then every `ms`. Pass key = null to switch it off.
// A new key (for example a different student) restarts it and ignores late responses.
export function usePolling<T>(fn: () => Promise<T>, ms: number, key: string | null) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(key !== null);
  const fnRef = useRef(fn);
  const runRef = useRef<() => Promise<void>>(async () => {});

  useEffect(() => {
    fnRef.current = fn;
  });

  useEffect(() => {
    if (key === null) {
      setData(null);
      setError(null);
      setLoading(false);
      runRef.current = async () => {};
      return;
    }
    let cancelled = false;
    const run = async () => {
      try {
        const d = await fnRef.current();
        if (!cancelled) {
          setData(d);
          setError(null);
        }
      } catch (e) {
        if (!cancelled) setError(errorMessage(e));
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    runRef.current = run;
    setData(null);
    setLoading(true);
    run();
    const id = setInterval(run, ms);
    return () => {
      cancelled = true;
      clearInterval(id);
    };
  }, [key, ms]);

  const refresh = useCallback(() => runRef.current(), []);
  return { data, error, loading, refresh };
}

// Server-adjusted current time in ms, updated every second.
export function useServerNow(): number {
  const [now, setNow] = useState(serverNow());
  useEffect(() => {
    const id = setInterval(() => setNow(serverNow()), 1000);
    return () => clearInterval(id);
  }, []);
  return now;
}