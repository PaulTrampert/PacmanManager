import { useEffect, useRef } from "react";

/**
 * Returns a function that calls `callback` with the arguments of its latest call once `delayMs`
 * pass without another call. Each call cancels the one still waiting, and unmounting cancels any
 * call still waiting, so `callback` is never called after its component has gone.
 *
 * The function's identity changes when `callback` or `delayMs` does. Every identity shares one
 * pending call, so a call through a newer one still cancels a call waiting from an older one, and a
 * caller passing an inline `callback` still gets one call per pause. A waiting call runs the
 * `callback` it was made through.
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
  // The pending call has to outlive renders, so that every identity of the returned function
  // cancels the same one; it is state, not an input hidden from the dependency list.
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);

  useEffect(() => () => clearTimeout(timer.current), []);

  return (...args: Args) => {
    clearTimeout(timer.current);
    timer.current = setTimeout(() => callback(...args), delayMs);
  };
}
