import { useEffect, useId, useState } from "react";
import type { FieldValidator } from "../../api";
import { useFormContext } from "./FormContext";

/** What {@link useFormField} needs from the field's props. */
export interface UseFormFieldOptions<T> {
  /** The field's name, unique within its form. */
  name: string;
  /** The field's current value. */
  value: T;
  /** Checks the value in the browser. */
  validate: FieldValidator<T>;
  /** The API's message for the field, from the last request's validation `400` or a `409`. */
  serverError: string | undefined;
}

/** What {@link useFormField} gives the field to render. */
export interface FormFieldState {
  /** The field's element id. */
  id: string;
  /** Every message to show in place of the helper text, none while there is nothing to show. */
  errors: string[];
  /** Marks the field as left, after which its errors show. */
  onBlur: () => void;
}

/**
 * The behaviour of a form's field, whatever control renders it: it reports to the enclosing `Form`
 * whether its value fails validation, and shows every failing rule's message only once the field
 * has been left or the form submitted, so nothing complains while a value is first entered. The
 * browser's messages take the place of the API's, which shows otherwise.
 */
export function useFormField<T>({
  name,
  value,
  validate,
  serverError,
}: UseFormFieldOptions<T>): FormFieldState {
  const { submitted, setFieldFailing } = useFormContext();
  const [left, setLeft] = useState(false);
  const id = useId();

  const messages = validate(value);
  const failing = messages.length > 0;

  useEffect(() => {
    setFieldFailing(name, failing);
    // A field that goes away blocks submitting no longer.
    return () => setFieldFailing(name, false);
  }, [name, failing, setFieldFailing]);

  const shown = (left || submitted) && failing;

  return {
    id,
    errors: shown ? messages : serverError === undefined ? [] : [serverError],
    onBlur: () => setLeft(true),
  };
}
