import type { ReactNode } from "react";

/**
 * The props every form control takes, whatever it renders: a `Form<Type>Field` such as
 * `FormTextField`. A control only renders them. The field that renders it decides its errors, and
 * when they show, from `useFormField`; a control adds only what its own kind needs, such as a
 * select's options, and `FormField` passes those through.
 */
export interface FormControlProps<T> {
  /** The control's element id. */
  id: string;
  /** The field's name, unique within its form. */
  name: string;
  /** The field's label. */
  label: string;
  /** The field's current value. */
  value: T;
  /** Called with the new value as it is edited. */
  onChange: (value: T) => void;
  /** Called when the control is left, after which its errors show. */
  onBlur: () => void;
  /** Every message to show in place of the helper text; empty while there is none to show. */
  errors: string[];
  /** Shown below the control while it has no errors to show. */
  helperText?: ReactNode;
  /** Disables the control. */
  disabled?: boolean;
}
