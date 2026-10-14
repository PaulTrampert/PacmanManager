import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import type { ReactNode } from "react";
import { ProblemDetailsAlert } from "../ProblemDetailsAlert";
import { FormContext } from "./FormContext";
import { useForm } from "./useForm";

/** The props {@link Form} takes. */
export interface FormProps {
  /** The form's fields: `FormField`s, and any other controls. */
  children: ReactNode;
  /** The submit button's label, such as **Create**. */
  submitLabel: string;
  /** The submit button's label while the request is in flight, such as **Creating…**. */
  pendingLabel: string;
  /** Called when the form is submitted and every `FormField` in it is valid. */
  onSubmit: () => void;
  /** Called when **Cancel** is chosen. */
  onCancel: () => void;
  /** Whether the submitted request is in flight. While it is, the submit button is disabled. */
  pending?: boolean;
  /** Disables the submit button alone, as while nothing has been changed. */
  submitDisabled?: boolean;
  /**
   * An error to show in a `ProblemDetailsAlert` above the fields, such as a failed request's. Leave
   * it out for one the form shows on its fields instead.
   */
  error?: unknown;
  /** Messages to list in the alert: a validation `400`'s entries that name none of the fields. */
  errorMessages?: readonly string[];
}

/**
 * A form: an optional `ProblemDetailsAlert`, the fields, and a row of a contained submit button and
 * **Cancel**. The browser's own validation is off. Submitting shows every `FormField`'s error, and
 * calls `onSubmit` only when there are none; the form never sends anything itself.
 */
export function Form({
  children,
  submitLabel,
  pendingLabel,
  onSubmit,
  onCancel,
  pending = false,
  submitDisabled = false,
  error,
  errorMessages,
}: FormProps) {
  const form = useForm({ onSubmit, pending, submitDisabled });
  return (
    <FormContext value={form.context}>
      <Box
        component="form"
        noValidate
        onSubmit={form.onSubmit}
        sx={{ maxWidth: 480 }}
      >
        <Stack spacing={3}>
          {error !== undefined && error !== null && (
            <ProblemDetailsAlert error={error} messages={errorMessages} />
          )}
          {children}
          <Stack direction="row" spacing={2}>
            <Button
              type="submit"
              variant="contained"
              disabled={form.submitDisabled}
            >
              {pending ? pendingLabel : submitLabel}
            </Button>
            <Button onClick={onCancel}>Cancel</Button>
          </Stack>
        </Stack>
      </Box>
    </FormContext>
  );
}
