import { useCallback, useMemo, useState, type FormEvent } from "react";
import type { FormContextValue } from "./FormContext";

/** What {@link useForm} needs from the form's props. */
export interface UseFormOptions {
  /** Called when the form is submitted with every field valid. */
  onSubmit: () => void;
  /** Whether the submitted request is in flight. */
  pending: boolean;
  /** Disables submitting, as while nothing has changed. */
  submitDisabled: boolean;
}

/** What {@link useForm} gives the form to render. */
export interface FormState {
  /** The context the form's fields read and report their errors through. */
  context: FormContextValue;
  /** Handles the `<form>`'s submit event. */
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  /** Whether the submit button is disabled. */
  submitDisabled: boolean;
}

/**
 * The behaviour of `Form`: its fields report their errors to it, submitting shows every field's
 * error, and `onSubmit` is called only when no field has one and nothing disables submitting.
 */
export function useForm({
  onSubmit,
  pending,
  submitDisabled,
}: UseFormOptions): FormState {
  const [submitted, setSubmitted] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<
    Readonly<Record<string, string>>
  >({});

  // Stable, since every field lists it as an effect's dependency.
  const setFieldError = useCallback(
    (name: string, error: string | undefined) =>
      setFieldErrors((current) => {
        if (current[name] === error) {
          return current;
        }
        const next = { ...current };
        if (error === undefined) {
          delete next[name];
        } else {
          next[name] = error;
        }
        return next;
      }),
    [],
  );

  const context = useMemo(
    () => ({ submitted, setFieldError }),
    [submitted, setFieldError],
  );

  const disabled = pending || submitDisabled;

  return {
    context,
    onSubmit: (event) => {
      // The browser never submits the form itself: the page decides what submitting does.
      event.preventDefault();
      if (disabled) {
        return;
      }
      setSubmitted(true);
      if (Object.keys(fieldErrors).length === 0) {
        onSubmit();
      }
    },
    submitDisabled: disabled,
  };
}
