import Box from "@mui/material/Box";
import TextField from "@mui/material/TextField";
import type { FormControlProps } from "./FormControlProps";

/** The props {@link FormTextField} takes. */
export type FormTextFieldProps = FormControlProps<string>;

/**
 * An outlined text field: the control `FormField` renders unless given another. Each of its errors
 * shows on a line of its own, in place of the helper text.
 */
export function FormTextField({
  id,
  name,
  label,
  value,
  onChange,
  onBlur,
  errors,
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
      error={errors.length > 0}
      helperText={
        errors.length > 0
          ? errors.map((message) => (
              <Box component="span" key={message} sx={{ display: "block" }}>
                {message}
              </Box>
            ))
          : helperText
      }
      disabled={disabled}
      fullWidth
    />
  );
}
