import TextField from "@mui/material/TextField";
import type { FormControlProps } from "./FormControlProps";

/** The props {@link FormTextField} takes. */
export type FormTextFieldProps = FormControlProps<string>;

/** An outlined text field: the control `FormField` renders unless given another. */
export function FormTextField({
  id,
  name,
  label,
  value,
  onChange,
  onBlur,
  error,
  helperText,
  disabled,
}: FormTextFieldProps) {
  return (
    <TextField
      id={id}
      name={name}
      label={label}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      onBlur={onBlur}
      error={error !== undefined}
      helperText={error ?? helperText}
      disabled={disabled}
      fullWidth
    />
  );
}
