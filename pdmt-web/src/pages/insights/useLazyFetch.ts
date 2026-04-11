import { useState, useEffect } from "react";
import { isAbortError, getErrorMessage } from "@/lib/utils";

export function useLazyFetch<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  initialValue: T,
  deps: unknown[],
  isActive: boolean,
): { data: T; loading: boolean; error: string | null } {
  const [data, setData] = useState<T>(initialValue);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    const controller = new AbortController();
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await fetcher(controller.signal));
      } catch (err: unknown) {
        if (isAbortError(err)) return;
        setError(getErrorMessage(err));
        console.error(err);
      } finally {
        setLoading(false);
      }
    })();
    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [shouldLoad, ...deps]);

  return { data, loading, error };
}
