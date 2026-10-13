import MenuItem from "@mui/material/MenuItem";
import type { SxProps, Theme } from "@mui/material/styles";
import TextField from "@mui/material/TextField";

/** What the MUI `Select` inside {@link FilterSelect} is given for **All**. */
const all = "";

/** One of a {@link FilterSelect}'s options besides **All**. */
export interface FilterSelectOption<T extends string> {
  /** The value reported when this option is chosen. */
  value: T;
  /** The text shown for this option. */
  label: string;
}

/** The props {@link FilterSelect} takes. */
export interface FilterSelectProps<T extends string> {
  /** The filter's name, shown as the field's label. */
  label: string;
  /** The options besides **All**, in the order they are shown. */
  options: readonly FilterSelectOption<T>[];
  /** The chosen option's value, or `undefined` for **All**. */
  value: T | undefined;
  /** Called with the chosen option's value, or with `undefined` when **All** is chosen. */
  onChange: (value: T | undefined) => void;
  /**
   * Disables the filter on this value, which must be one of `options`, however many options there
   * are. Without it the filter is disabled on **All** only when it has one option or none.
   */
  disabledValue?: T;
  /** Styles for the field, such as its width. */
  sx?: SxProps<Theme>;
}

/**
 * An outlined select for one filter: **All**, then the given options.
 *
 * A filter that cannot narrow anything — one option or none — is shown disabled on **All**, and
 * `disabledValue` disables it on that value instead. A disabled filter shows only what it is
 * disabled on, whatever `value` it is given, and never calls `onChange`.
 */
export function FilterSelect<T extends string>({
  label,
  options,
  value,
  onChange,
  disabledValue,
  sx,
}: FilterSelectProps<T>) {
  const disabled = disabledValue !== undefined || options.length <= 1;
  const shown = disabled ? disabledValue : value;

  return (
    <TextField
      select
      size="small"
      label={label}
      value={shown ?? all}
      disabled={disabled}
      onChange={(event) => {
        const chosen = event.target.value;
        onChange(chosen === all ? undefined : (chosen as T));
      }}
      sx={sx}
      slotProps={{
        inputLabel: { shrink: true },
        select: { displayEmpty: true },
      }}
    >
      <MenuItem value={all}>All</MenuItem>
      {options.map((option) => (
        <MenuItem key={option.value} value={option.value}>
          {option.label}
        </MenuItem>
      ))}
    </TextField>
  );
}
