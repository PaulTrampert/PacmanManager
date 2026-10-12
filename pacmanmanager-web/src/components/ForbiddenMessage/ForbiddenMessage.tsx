import Alert from "@mui/material/Alert";

/** What {@link ForbiddenMessage} says. */
export const forbiddenText = "You don't have permission to do that.";

/**
 * Shown in place of a page or form when the API answers `403`: the caller is known, and may not do
 * what they asked.
 */
export function ForbiddenMessage() {
  return <Alert severity="warning">{forbiddenText}</Alert>;
}
