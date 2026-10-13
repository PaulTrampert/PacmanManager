import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { ReactNode } from "react";

/** The props {@link EmptyState} takes. */
export interface EmptyStateProps {
  /** What is empty, said in a few words, such as "No repositories yet". */
  heading: string;
  /** A sentence saying why the list is empty, or what to do about it. */
  message: string;
  /**
   * What the reader can do next — a button or a link, styled by the caller. Nothing is shown below
   * the message without one.
   */
  action?: ReactNode;
}

/**
 * Shown in place of a list that has nothing in it: a heading, a message and an optional action,
 * centred in an outlined box. It knows nothing about what the list holds; the page that shows it
 * chooses the text and the action.
 */
export function EmptyState({ heading, message, action }: EmptyStateProps) {
  return (
    <Paper variant="outlined" sx={{ px: 3, py: 8 }}>
      <Stack spacing={1} sx={{ alignItems: "center", textAlign: "center" }}>
        <Typography variant="h6" component="h2">
          {heading}
        </Typography>
        <Typography color="text.secondary">{message}</Typography>
        {action && <Stack sx={{ pt: 2 }}>{action}</Stack>}
      </Stack>
    </Paper>
  );
}
