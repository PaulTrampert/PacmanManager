import Box from "@mui/material/Box";
import MenuItem from "@mui/material/MenuItem";
import TextField from "@mui/material/TextField";
import type { FormControlProps } from "./FormControlProps";
import { inOptionOrder } from "./inOptionOrder";

/** One of a {@link FormMultiSelectField}'s options. */
export interface FormMultiSelectOption {
  /** The value the field holds while this option is selected. */
  value: string;
  /** The text shown for this option, in the menu and among the selected values. */
  label: string;
}

/** The props {@link FormMultiSelectField} takes. */
export interface FormMultiSelectFieldProps extends FormControlProps<string[]> {
  /** The options to choose from, in the order they are shown and reported. */
  options: readonly FormMultiSelectOption[];
}

/**
 * An outlined select of any number of `options`, showing the selected values by their labels. A
 * selected value not among the options is shown as it is. `onChange` reports the selected values in
 * the options' order, followed by any not among them.
 */
export function FormMultiSelectField({
  id,
  name,
  label,
  value,
  onChange,
  onBlur,
  errors,
  helperText,
  disabled,
  options,
}: FormMultiSelectFieldProps) {
  const labels = new Map(options.map((option) => [option.value, option.label]));
  return (
    <TextField
      select
      id={id}
      name={name}
      label={label}
      value={value}
      onChange={(event) => {
        // A multiple select reports an array, but MUI types the event's value as a string, which
        // it is only when the browser autofills a comma-separated one.
        const selected: unknown = event.target.value;
        onChange(
          inOptionOrder(
            typeof selected === "string"
              ? selected.split(",")
              : (selected as string[]),
            options,
          ),
        );
      }}
      // MUI's Select calls this only when it loses focus with its menu closed, so moving into the
      // menu and back does not count as leaving the field.
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
      slotProps={{
        select: {
          multiple: true,
          renderValue: (selected) =>
            (selected as string[])
              .map(
                (selectedValue) => labels.get(selectedValue) ?? selectedValue,
              )
              .join(", "),
        },
      }}
    >
      {options.map((option) => (
        <MenuItem key={option.value} value={option.value}>
          {option.label}
        </MenuItem>
      ))}
    </TextField>
  );
}
