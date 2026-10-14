import TextField from "@mui/material/TextField";
import type { FormControlProps } from "./FormControlProps";
import { useFormField } from "./useFormField";

/** The props {@link FormTextField} takes. */
export type FormTextFieldProps = FormControlProps<string>;

/**
 * An outlined text field inside a `Form`, checked by any validator. It is the control `FormField`
 * renders unless given another.
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
  const field = useFormField({ name, value, validate, serverError });
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
