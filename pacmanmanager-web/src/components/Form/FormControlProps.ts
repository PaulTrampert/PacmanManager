import type { ReactNode } from "react";
import type { FieldValidator } from "../../api";

/**
 * The props every form control takes, whatever it renders: a `Form<Type>Field` such as
 * `FormTextField`. `FormField` gives a control these and passes the rest of its props through, so
 * a control adds only what its own kind needs, such as a select's options. A control gets its
 * behaviour from `useFormField` and only renders.
 */
export interface FormControlProps<T> {
  /** The field's name, unique within its form. */
  name: string;
  /** The field's label. */
  label: string;
  /** Checks the value in the browser. */
  validate: FieldValidator<T>;
  /** The field's current value. */
  value: T;
  /** Called with the new value as it is edited. */
  onChange: (value: T) => void;
  /** Shown below the field while it has no error to show. */
  helperText?: ReactNode;
  /** The API's message for the field, from the last request's validation `400` or a `409`. */
  serverError?: string;
  /** Disables the field. */
  disabled?: boolean;
}
