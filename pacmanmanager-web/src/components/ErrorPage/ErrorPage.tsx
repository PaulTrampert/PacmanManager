import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { ApiError } from "../../api";
import { ForbiddenMessage } from "../ForbiddenMessage";
import { ProblemDetailsAlert } from "../ProblemDetailsAlert";

/** The props {@link ErrorPage} takes. */
export interface ErrorPageProps {
  /** Why the page could not be shown: an `ApiError`, a network failure or a rendering exception. */
  error: unknown;
  /** Called by the **Try again** action, to load the page again. */
  onRetry: () => void;
}

/**
 * Shown in place of a page that failed to load or render. A `403` is shown as the forbidden
 * message, since asking again gets the same answer; anything else is described, with a **Try
 * again** action.
 */
export function ErrorPage({ error, onRetry }: ErrorPageProps) {
  if (error instanceof ApiError && error.status === 403) {
    return <ForbiddenMessage />;
  }
  return (
    <Stack spacing={2} sx={{ alignItems: "flex-start" }}>
      <Typography variant="h4" component="h1">
        Something went wrong
      </Typography>
      <ProblemDetailsAlert error={error} />
      <Button variant="contained" onClick={onRetry}>
        Try again
      </Button>
    </Stack>
  );
}
