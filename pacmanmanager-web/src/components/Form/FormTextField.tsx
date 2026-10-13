import TextField from "@mui/material/TextField";
import type { ReactNode } from "react";
import type { FieldValidator } from "../../api";
import { useFormTextField } from "./useFormTextField";

/** The props {@link FormTextField} takes. */
export interface FormTextFieldProps {
  /** The field's name, unique within its form. */
  name: string;
  /** The field's label. */
  label: string;
  /** Checks the value in the browser. */
  validate: FieldValidator;
  /** The field's current value. */
  value: string;
  /** Called with the new value as it is edited. */
  onChange: (value: string) => void;
  /** Shown below the field while it has no error to show. */
  helperText?: ReactNode;
  /** The API's message for the field, from the last request's validation `400` or a `409`. */
  serverError?: string;
  /** Disables the field. */
  disabled?: boolean;
}

/**
 * An outlined text field inside a `Form`, checked by any validator. Forms use `FormField`, which
 * builds the validator from the OpenAPI document's rules; this is what it renders.
 */
export function FormTextField({
  name,
  label,
  validate,
  value,
  onChange,
  helperText,
  serverError,
  disabled,
}: FormTextFieldProps) {
  const field = useFormTextField({ name, value, validate, serverError });
  return (
    <TextField
      id={field.id}
      name={name}
      label={label}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      onBlur={field.onBlur}
      error={field.error !== undefined}
      helperText={field.error ?? helperText}
      disabled={disabled}
      fullWidth
    />
  );
}
