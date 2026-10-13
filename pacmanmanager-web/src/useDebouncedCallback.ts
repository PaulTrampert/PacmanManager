import { useCallback, useEffect, useRef } from "react";

/**
 * Returns a function that calls `callback` with the arguments of its latest call once `delayMs`
 * pass without another call. Each call cancels the one still waiting, and unmounting cancels any
 * call still waiting, so `callback` is never called after its component has gone.
 *
 * The function keeps its identity across renders while `delayMs` does, and always calls the
 * `callback` of the latest render, so callers need not keep `callback` stable.
 *
 * @param callback the function to call once calls stop.
 * @param delayMs how long calls must stop for, in milliseconds.
 *
 * @example
 * const search = useDebouncedCallback((text: string) => onSearch(text), 300);
 * <input onChange={(event) => search(event.target.value)} />
 */
export function useDebouncedCallback<Args extends unknown[]>(
  callback: (...args: Args) => void,
  delayMs: number,
): (...args: Args) => void {
  const callbackRef = useRef(callback);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  useEffect(() => () => clearTimeout(timer.current), []);

  return useCallback(
    (...args: Args) => {
      clearTimeout(timer.current);
      timer.current = setTimeout(() => callbackRef.current(...args), delayMs);
    },
    [delayMs],
  );
}
