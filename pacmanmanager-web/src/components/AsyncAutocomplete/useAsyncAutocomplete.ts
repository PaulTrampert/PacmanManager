import type { AutocompleteInputChangeReason } from "@mui/material/Autocomplete";
import { useRef, useState } from "react";
import { useDebouncedCallback } from "../../useDebouncedCallback";
import { visibleOptions } from "./visibleOptions";

/** How long typing must stop for before an `AsyncAutocomplete` searches. */
export const searchDelayMs = 300;

/** What {@link useAsyncAutocomplete} needs from an `AsyncAutocomplete`'s props. */
export interface UseAsyncAutocompleteOptions<T> {
  /** The chosen option, or `undefined` for **All**. */
  value: T | undefined;
  /** Called with the option chosen, or `undefined` when the field is cleared back to **All**. */
  onChange: (value: T | undefined) => void;
  /** Called with the text typed so far, {@link searchDelayMs} after typing stops. */
  onSearch: (text: string) => void;
  /** The options the latest search returned. */
  options: readonly T[];
  /** Options always offered first when their search text contains the typed text. */
  pinnedOptions: readonly T[];
  /** How many options there are in all, when the caller knows. */
  totalOptions: number | undefined;
  /** Identifies an option. */
  getOptionKey: (option: T) => string;
  /** The text of a pinned option that typed text is matched against. */
  getOptionSearchText: (option: T) => string;
}

/** What an `AsyncAutocomplete` renders, as {@link useAsyncAutocomplete} works it out. */
export interface AsyncAutocompleteState<T> {
  /** Whether the field is disabled on **All**, because there is one option or none. */
  disabled: boolean;
  /** The value to show: `null` for **All**, and always while {@link disabled}. */
  value: T | null;
  /** The options to list, matching pinned options first, then the search results. */
  options: T[];
  /** Whether the divider between pinned options and search results goes after `option`. */
  hasDividerAfter: (option: T) => boolean;
  /** Whether two options are the same option. */
  isOptionEqualToValue: (option: T, chosen: T) => boolean;
  /** Handles the field's choice changing, to an option or to nothing. */
  onChange: (chosen: T | null) => void;
  /** Handles the field's text changing, for the reason MUI gives. */
  onInputChange: (text: string, reason: AutocompleteInputChangeReason) => void;
}

/**
 * The behaviour of an `AsyncAutocomplete`.
 *
 * Typing searches through `onSearch` {@link searchDelayMs} after it stops, once per pause. Any
 * other change to the text (choosing an option, clearing the field, leaving it) empties the typed
 * text and searches again with `""`, unless the last search was already for `""`. A search for the
 * text last searched for is never repeated. With one option or none, the field is disabled on
 * **All**.
 */
export function useAsyncAutocomplete<T>({
  value,
  onChange,
  onSearch,
  options,
  pinnedOptions,
  totalOptions,
  getOptionKey,
  getOptionSearchText,
}: UseAsyncAutocompleteOptions<T>): AsyncAutocompleteState<T> {
  const disabled = totalOptions !== undefined && totalOptions <= 1;
  const [typed, setTyped] = useState("");
  const lastSearched = useRef("");

  const search = useDebouncedCallback((text: string) => {
    if (text !== lastSearched.current) {
      lastSearched.current = text;
      onSearch(text);
    }
  }, searchDelayMs);

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

  return {
    disabled,
    value: disabled ? null : (value ?? null),
    options: [...visible.pinned, ...visible.results],
    hasDividerAfter: (option) => getOptionKey(option) === dividerAfter,
    isOptionEqualToValue: (option, chosen) =>
      getOptionKey(option) === getOptionKey(chosen),
    onChange: (chosen) => onChange(chosen ?? undefined),
    onInputChange: (text, reason) => {
      // Only typing is a search; anything else MUI puts in the field (the chosen option's label,
      // or nothing once it is cleared or left) starts the list afresh.
      const searchText = reason === "input" ? text : "";
      setTyped(searchText);
      search(searchText);
    },
  };
}
