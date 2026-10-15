import FormControl from "@mui/material/FormControl";
import FormControlLabel from "@mui/material/FormControlLabel";
import FormHelperText from "@mui/material/FormHelperText";
import FormLabel from "@mui/material/FormLabel";
import Radio from "@mui/material/Radio";
import RadioGroup from "@mui/material/RadioGroup";
import Typography from "@mui/material/Typography";
import type { FormControlProps } from "./FormControlProps";

/** One of a {@link FormRadioGroupField}'s options. */
export interface FormRadioGroupOption<T> {
  /** The value reported when this option is chosen. */
  value: T;
  /** The option's label. */
  label: string;
  /** Shown beneath the option's label, describing it. */
  description?: string;
}

/** The props {@link FormRadioGroupField} takes. */
export interface FormRadioGroupFieldProps<T> extends FormControlProps<T> {
  /** The options, in the order they are shown. */
  options: readonly FormRadioGroupOption<T>[];
}

/**
 * A labelled group of radio buttons, one per option, of a value of any type: a control for
 * `FormField`. Each option's description shows beneath it, and the field's error or helper text
 * below the group. It calls `onBlur` once focus leaves the group, not as it moves between options.
 */
export function FormRadioGroupField<T>({
  id,
  name,
  label,
  value,
  onChange,
  onBlur,
  error,
  helperText,
  disabled,
  options,
}: FormRadioGroupFieldProps<T>) {
  const labelId = `${id}-label`;
  const helperTextId = `${id}-helper-text`;
  const shownHelperText = error ?? helperText;
  // The inputs carry each option's index, since a radio's value is a string and T may not be.
  const chosen = options.findIndex((option) => Object.is(option.value, value));

  return (
    <FormControl error={error !== undefined} disabled={disabled}>
      <FormLabel id={labelId}>{label}</FormLabel>
      <RadioGroup
        id={id}
        name={name}
        value={chosen === -1 ? "" : String(chosen)}
        onChange={(event) =>
          onChange(options[Number(event.target.value)].value)
        }
        onBlur={(event) => {
          // Moving between the options blurs one radio for another; only leaving the group counts.
          if (!event.currentTarget.contains(event.relatedTarget)) onBlur();
        }}
        aria-labelledby={labelId}
        aria-describedby={shownHelperText ? helperTextId : undefined}
        aria-invalid={error !== undefined}
      >
        {options.map((option, index) => {
          const descriptionId = `${id}-option-${index}-description`;
          return (
            <div key={index}>
              <FormControlLabel
                value={String(index)}
                label={option.label}
                control={
                  <Radio
                    slotProps={{
                      input: {
                        "aria-describedby": option.description
                          ? descriptionId
                          : undefined,
                      },
                    }}
                  />
                }
              />
              {option.description && (
                <Typography
                  id={descriptionId}
                  variant="body2"
                  color="text.secondary"
                  // Beneath the label, past the radio's width.
                  sx={{ ml: 4, mt: -0.5, mb: 0.5 }}
                >
                  {option.description}
                </Typography>
              )}
            </div>
          );
        })}
      </RadioGroup>
      {shownHelperText && (
        <FormHelperText id={helperTextId}>{shownHelperText}</FormHelperText>
      )}
    </FormControl>
  );
}
