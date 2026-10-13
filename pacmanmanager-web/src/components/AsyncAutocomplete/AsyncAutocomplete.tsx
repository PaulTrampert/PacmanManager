import Autocomplete, {
  type AutocompleteInputChangeReason,
} from "@mui/material/Autocomplete";
import CircularProgress from "@mui/material/CircularProgress";
import Divider from "@mui/material/Divider";
import TextField from "@mui/material/TextField";
import { Fragment, useEffect, useRef, useState } from "react";
import { visibleOptions } from "./visibleOptions";

/** How long typing must stop for before {@link AsyncAutocomplete} searches. */
export const searchDelayMs = 300;

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
  const disabled = totalOptions !== undefined && totalOptions <= 1;
  const [typed, setTyped] = useState("");
  const search = useDebouncedSearch(onSearch);

  const visible = visibleOptions(
    pinnedOptions,
    options,
    typed,
    getOptionKey,
    getOptionSearchText,
  );
  const lastPinned = visible.pinned.at(-1);
  const dividerAfter =
    lastPinned !== undefined && visible.results.length > 0
      ? getOptionKey(lastPinned)
      : undefined;

  const handleInputChange = (
    text: string,
    reason: AutocompleteInputChangeReason,
  ) => {
    // Only typing is a search; anything else MUI puts in the field (the chosen option's label,
    // or nothing once it is cleared or left) starts the list afresh.
    const searchText = reason === "input" ? text : "";
    setTyped(searchText);
    search(searchText);
  };

  return (
    <Autocomplete<T>
      sx={{ width: 240 }}
      options={[...visible.pinned, ...visible.results]}
      // The search filters the results and visibleOptions the pinned options, so MUI must not.
      filterOptions={(shown) => shown}
      value={disabled ? null : (value ?? null)}
      onChange={(_, chosen) => onChange(chosen ?? undefined)}
      onInputChange={(_, text, reason) => handleInputChange(text, reason)}
      disabled={disabled}
      loading={loading}
      getOptionKey={getOptionKey}
      getOptionLabel={getOptionLabel}
      isOptionEqualToValue={(option, chosen) =>
        getOptionKey(option) === getOptionKey(chosen)
      }
      renderOption={({ key, ...props }, option) => (
        <Fragment key={key}>
          <li {...props}>{getOptionLabel(option)}</li>
          {getOptionKey(option) === dividerAfter && <Divider component="li" />}
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

/**
 * Returns a function that calls `onSearch` with the latest text it was given once `searchDelayMs`
 * pass without another call, and not at all when that text is what was last searched for.
 */
function useDebouncedSearch(onSearch: (text: string) => void) {
  const onSearchRef = useRef(onSearch);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const lastSearched = useRef("");

  useEffect(() => {
    onSearchRef.current = onSearch;
  }, [onSearch]);

  useEffect(() => () => clearTimeout(timer.current), []);

  return (text: string) => {
    clearTimeout(timer.current);
    if (text === lastSearched.current) {
      return;
    }
    timer.current = setTimeout(() => {
      lastSearched.current = text;
      onSearchRef.current(text);
    }, searchDelayMs);
  };
}
