import { useCallback, useEffect, useRef, useState } from "react";

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

  const show = useCallback(
    (next: Value) => {
      clearTimeout(timer.current);
      setValue(next);
      timer.current = setTimeout(() => setValue(resting), duration);
    },
    [resting, duration],
  );

  return { value, show };
}
