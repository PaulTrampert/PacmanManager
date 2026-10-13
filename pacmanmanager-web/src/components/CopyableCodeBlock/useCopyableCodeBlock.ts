import { useRef, useState, type RefObject } from "react";
import { useTemporaryFeedback } from "../../useTemporaryFeedback";

/** What a click on **Copy** last came to: nothing yet (or long enough ago), copied, or refused. */
export type CopyOutcome = "idle" | "copied" | "failed";

/** What {@link useCopyableCodeBlock} returns. */
export interface CopyableCodeBlockState {
  /** What the last click came to; reverts to `"idle"` after the feedback duration. */
  outcome: CopyOutcome;
  /** Whether the browser refused the last click, until **Copy** is next clicked. Not on the timer. */
  blocked: boolean;
  /** To be set on the element holding the text, so a refused copy can select exactly its contents. */
  block: RefObject<HTMLPreElement | null>;
  /** Puts the text on the clipboard; if it cannot, selects the block's contents instead. */
  copy: () => Promise<void>;
}

/**
 * The behaviour of `CopyableCodeBlock`: copying `text`, and the outcome and the failure message
 * that follow.
 *
 * @param text what **Copy** puts on the clipboard.
 * @param feedbackDuration how long `outcome` stays `"copied"` or `"failed"`, in milliseconds.
 */
export function useCopyableCodeBlock(
  text: string,
  feedbackDuration: number,
): CopyableCodeBlockState {
  const { value: outcome, show } = useTemporaryFeedback<CopyOutcome>(
    "idle",
    feedbackDuration,
  );
  const [blocked, setBlocked] = useState(false);
  const block = useRef<HTMLPreElement>(null);

  const copy = async () => {
    setBlocked(false);
    try {
      // `navigator.clipboard` is undefined outside a secure context, whatever its type says.
      if (!(navigator.clipboard as Clipboard | undefined)) {
        throw new Error("The clipboard API is unavailable.");
      }
      await navigator.clipboard.writeText(text);
      show("copied");
    } catch {
      if (block.current) {
        window.getSelection()?.selectAllChildren(block.current);
      }
      setBlocked(true);
      show("failed");
    }
  };

  return { outcome, blocked, block, copy };
}
