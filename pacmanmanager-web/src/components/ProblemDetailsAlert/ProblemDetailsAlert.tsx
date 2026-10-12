import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import { ApiError } from "../../api";

/** The props {@link ProblemDetailsAlert} takes. */
export interface ProblemDetailsAlertProps {
  /**
   * What failed: an `ApiError`, whose problem details' `title` and `detail` are shown, or any other
   * error, such as a network failure, shown by its message.
   */
  error: unknown;
  /**
   * Further messages to list below the detail: the validation errors of a form that has no field
   * to show them on, say.
   */
  messages?: readonly string[];
}

/** The title shown for an error that carries none of its own. */
export const fallbackErrorTitle = "An unexpected error occurred.";

function describe(error: unknown): { title: string; detail?: string } {
  if (error instanceof ApiError) {
    return {
      title: error.title ?? `The request failed with status ${error.status}.`,
      detail: error.detail,
    };
  }
  if (error instanceof Error && error.message) {
    return { title: fallbackErrorTitle, detail: error.message };
  }
  return { title: fallbackErrorTitle };
}

/**
 * An error alert describing a failed request: the response's problem details `title` and `detail`,
 * and any further `messages` as a list.
 */
export function ProblemDetailsAlert({
  error,
  messages = [],
}: ProblemDetailsAlertProps) {
  const { title, detail } = describe(error);
  return (
    <Alert severity="error">
      <AlertTitle>{title}</AlertTitle>
      {detail && <Typography variant="body2">{detail}</Typography>}
      {messages.length > 0 && (
        <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
          {messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </Box>
      )}
    </Alert>
  );
}
