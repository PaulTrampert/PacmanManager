import Autocomplete from "@mui/material/Autocomplete";
import CircularProgress from "@mui/material/CircularProgress";
import Divider from "@mui/material/Divider";
import TextField from "@mui/material/TextField";
import { Fragment } from "react";
import { searchDelayMs, useAsyncAutocomplete } from "./useAsyncAutocomplete";

export { searchDelayMs };

/** The props {@link AsyncAutocomplete} takes. */
export interface AsyncAutocompleteProps<T> {
  /** The field's label. */
  label: string;
  /** The chosen option, or `undefined` for **All**. */
  value: T | undefined;
  /** Called with the option chosen, or `undefined` when the field is cleared back to **All**. */
  onChange: (value: T | undefined) => void;
  /**
   * Called with the text typed so far, {@link searchDelayMs} after typing stops, once per pause.
   * Choosing an option or leaving the field empties the text, and searches again with `""` if the
   * last search was for anything else. The caller answers with new {@link options}.
   */
  onSearch: (text: string) => void;
  /** The options the latest search returned. */
  options: readonly T[];
  /**
   * Options always offered first, followed by a divider, when their search text contains the
   * typed text. A search result that is one of them is never listed again.
   */
  pinnedOptions?: readonly T[];
  /** Whether a search is in flight. */
  loading?: boolean;
  /**
   * How many options there are in all, when the caller knows. With one or none the field is
   * disabled on **All** and reports no value.
   */
  totalOptions?: number;
  /** Identifies an option. Two options with the same key are the same option. */
  getOptionKey: (option: T) => string;
  /** The text an option is listed as, and shown as in the field once chosen. */
  getOptionLabel: (option: T) => string;
  /**
   * The text of a pinned option that typed text is matched against; its label unless given.
   */
  getOptionSearchText?: (option: T) => string;
}

/**
 * An outlined autocomplete for one filter, whose options come from a search the caller runs.
 *
 * It searches through {@link AsyncAutocompleteProps.onSearch} {@link searchDelayMs} after typing
 * stops, lists any matching pinned options first, then the search results, and reads **All** when
 * nothing is chosen. Told there is one option or none, it is disabled on **All**. It never calls
 * the API itself.
 */
export function AsyncAutocomplete<T>({
  label,
  value,
  onChange,
  onSearch,
  options,
  pinnedOptions = [],
  loading = false,
  totalOptions,
  getOptionKey,
  getOptionLabel,
  getOptionSearchText = getOptionLabel,
}: AsyncAutocompleteProps<T>) {
  const autocomplete = useAsyncAutocomplete({
    value,
    onChange,
    onSearch,
    options,
    pinnedOptions,
    totalOptions,
    getOptionKey,
    getOptionSearchText,
  });

  return (
    <Autocomplete<T>
      sx={{ width: 240 }}
      options={autocomplete.options}
      // The search filters the results and visibleOptions the pinned options, so MUI must not.
      filterOptions={(shown) => shown}
      value={autocomplete.value}
      onChange={(_, chosen) => autocomplete.onChange(chosen)}
      onInputChange={(_, text, reason) =>
        autocomplete.onInputChange(text, reason)
      }
      disabled={autocomplete.disabled}
      loading={loading}
      getOptionKey={getOptionKey}
      getOptionLabel={getOptionLabel}
      isOptionEqualToValue={autocomplete.isOptionEqualToValue}
      renderOption={({ key, ...props }, option) => (
        <Fragment key={key}>
          <li {...props}>{getOptionLabel(option)}</li>
          {autocomplete.hasDividerAfter(option) && <Divider component="li" />}
        </Fragment>
      )}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          placeholder="All"
          sx={{
            // **All** reads as a value, not a hint, until the field is focused for typing.
            "& .MuiInputBase-input::placeholder": {
              opacity: 1,
              color: "text.primary",
            },
            "& .MuiInputBase-input.Mui-disabled::placeholder": {
              color: "text.disabled",
            },
            "& .MuiInputBase-input:focus::placeholder": { opacity: 0 },
          }}
          slotProps={{
            ...params.slotProps,
            inputLabel: { ...params.slotProps.inputLabel, shrink: true },
            input: {
              ...params.slotProps.input,
              endAdornment: (
                <>
                  {loading && (
                    <CircularProgress
                      color="inherit"
                      size={20}
                      aria-label="Searching"
                    />
                  )}
                  {params.slotProps.input.endAdornment}
                </>
              ),
            },
          }}
        />
      )}
    />
  );
}
