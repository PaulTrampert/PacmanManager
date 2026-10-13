import CheckIcon from "@mui/icons-material/Check";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Typography from "@mui/material/Typography";
import { useEffect, useRef, useState, type ReactNode } from "react";

/** The props {@link CopyableCodeBlock} takes. */
export interface CopyableCodeBlockProps {
  /** The block's text, copied exactly as given, newlines included. */
  text: string;
  /** Shown to the left of the **Copy** button: an instruction saying what to do with the text. */
  description?: ReactNode;
  /** How long **Copy** reads **Copied** or **Couldn't copy** before reverting, in milliseconds. */
  feedbackDuration?: number;
}

/** How long **Copy** shows the outcome of a click by default, in milliseconds. */
export const defaultFeedbackDuration = 3000;

/** What {@link CopyableCodeBlock} says below the block when the browser would not copy its text. */
export const copyFailedText =
  "Your browser blocked copying. The text is selected; press Ctrl+C to copy it.";

type Outcome = "idle" | "copied" | "failed";

const labels: Record<Outcome, string> = {
  idle: "Copy",
  copied: "Copied",
  failed: "Couldn't copy",
};

const colors = {
  idle: "primary",
  copied: "success",
  failed: "error",
} as const satisfies Record<Outcome, string>;

/**
 * A read-only monospace block of text with a **Copy** button. **Copy** puts the text on the
 * clipboard and reads **Copied** for a few seconds. When the clipboard API is missing, as it is on
 * a page served over plain `http://` from anywhere but `localhost`, or the browser refuses the
 * write, it instead selects exactly the block's text, so Ctrl+C copies it, reads **Couldn't copy**
 * for a few seconds, and says below the block what happened until it is next clicked.
 */
export function CopyableCodeBlock({
  text,
  description,
  feedbackDuration = defaultFeedbackDuration,
}: CopyableCodeBlockProps) {
  const [outcome, setOutcome] = useState<Outcome>("idle");
  const [blocked, setBlocked] = useState(false);
  const block = useRef<HTMLPreElement>(null);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);

  useEffect(() => () => clearTimeout(timer.current), []);

  const show = (next: Outcome) => {
    clearTimeout(timer.current);
    setOutcome(next);
    timer.current = setTimeout(() => setOutcome("idle"), feedbackDuration);
  };

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

  return (
    <Box>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 2,
          mb: 1.5,
        }}
      >
        <Typography variant="body2" color="text.secondary" component="div">
          {description}
        </Typography>
        <Button
          variant="outlined"
          size="small"
          color={colors[outcome]}
          endIcon={outcome === "copied" ? <CheckIcon /> : undefined}
          onClick={() => void copy()}
          sx={{ flexShrink: 0 }}
        >
          {labels[outcome]}
        </Button>
      </Box>
      <Box
        component="pre"
        ref={block}
        sx={{
          m: 0,
          p: 2,
          borderRadius: 1,
          bgcolor: "action.hover",
          fontFamily: "monospace",
          fontSize: "0.875rem",
          overflowX: "auto",
          userSelect: "text",
        }}
      >
        {text}
      </Box>
      {blocked && (
        <Typography
          role="alert"
          variant="caption"
          color="error"
          component="p"
          sx={{ mt: 1 }}
        >
          {copyFailedText}
        </Typography>
      )}
    </Box>
  );
}
