import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";

/** What {@link useTemporaryFeedback} returns. */
export interface TemporaryFeedback<Value> {
  /** The value being shown: the last one passed to `show` until its time is up, else the resting one. */
  value: Value;
  /** Shows `next` for the hook's duration, then reverts to the resting value. A later call restarts the time. */
  show: (next: Value) => void;
}

/**
 * State that reverts by itself: `show` swaps in a value for `duration` milliseconds, after which
 * `value` is `resting` again. The pending revert is cancelled when `show` is called again and when
 * the component unmounts.
 *
 * `show` keeps its identity for the life of the component, so callers may list it as a
 * dependency or pass it to a memoized child. It reads `resting` and `duration` as they are when it
 * is called and when its revert fires, so a revert restores the latest `resting`.
 *
 * @param resting the value shown when no feedback is.
 * @param duration how long `show`n values last, in milliseconds.
 *
 * @example
 * const { value: label, show } = useTemporaryFeedback("Copy", 3000);
 * <Button onClick={() => show("Copied")}>{label}</Button>
 */
export function useTemporaryFeedback<Value>(
  resting: Value,
  duration: number,
): TemporaryFeedback<Value> {
  const [value, setValue] = useState(resting);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);

  useEffect(() => () => clearTimeout(timer.current), []);

  // The latest-value pattern: `show` reads its inputs through this ref instead of its dependency
  // list, so it never changes. React forbids writing a ref during render, so it is kept current
  // in a layout effect, which runs before any event handler or effect could call `show`.
  const latest = useRef({ resting, duration });
  useLayoutEffect(() => {
    latest.current = { resting, duration };
  });

  const show = useCallback((next: Value) => {
    clearTimeout(timer.current);
    setValue(next);
    timer.current = setTimeout(
      () => setValue(latest.current.resting),
      latest.current.duration,
    );
  }, []);

  return { value, show };
}
