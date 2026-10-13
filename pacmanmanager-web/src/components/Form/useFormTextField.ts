import { useEffect, useId, useState } from "react";
import type { FieldValidator } from "../../api";
import { useFormContext } from "./FormContext";

/** What {@link useFormTextField} needs from the field's props. */
export interface UseFormTextFieldOptions {
  /** The field's name, unique within its form. */
  name: string;
  /** The field's current value. */
  value: string;
  /** Checks the value in the browser. */
  validate: FieldValidator;
  /** The API's message for the field, from the last request's validation `400` or a `409`. */
  serverError: string | undefined;
}

/** What {@link useFormTextField} gives the field to render. */
export interface FormTextFieldState {
  /** The field's element id. */
  id: string;
  /** The error to show in place of the helper text, if any. */
  error: string | undefined;
  /** Marks the field as left, after which its error shows. */
  onBlur: () => void;
}

/**
 * The behaviour of a form's text field: it reports its validation error to the enclosing `Form`,
 * and shows it only once the field has been left or the form submitted, so nothing complains while
 * a value is first typed. A browser error takes the place of the API's, which shows otherwise.
 */
export function useFormTextField({
  name,
  value,
  validate,
  serverError,
}: UseFormTextFieldOptions): FormTextFieldState {
  const { submitted, setFieldError } = useFormContext();
  const [left, setLeft] = useState(false);
  const id = useId();

  const error = validate(value);

  useEffect(() => {
    setFieldError(name, error);
    // A field that goes away blocks submitting no longer.
    return () => setFieldError(name, undefined);
  }, [name, error, setFieldError]);

  const shown = left || submitted ? error : undefined;

  return {
    id,
    error: shown ?? serverError,
    onBlur: () => setLeft(true),
  };
}
