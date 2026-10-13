/** The options an {@link AsyncAutocomplete} lists, in the two groups it shows. */
export interface VisibleOptions<T> {
  /** The pinned options that match the typed text, shown first. */
  pinned: T[];
  /** The search results, less any pinned option, shown after the divider. */
  results: T[];
}

/**
 * Works out which options an {@link AsyncAutocomplete} lists for the text typed so far.
 *
 * A pinned option is shown only when its search text contains the typed text, ignoring case, and
 * blank text matches every one. A search result is dropped whenever it is one of the pinned
 * options, shown or not, so a pinned option never appears twice.
 *
 * @param pinnedOptions the options always offered first, when they match.
 * @param searchResults the options the latest search returned.
 * @param text the text typed so far.
 * @param getOptionKey identifies an option, for telling pinned options and results apart.
 * @param getOptionSearchText the text of an option that typed text is matched against.
 */
export function visibleOptions<T>(
  pinnedOptions: readonly T[],
  searchResults: readonly T[],
  text: string,
  getOptionKey: (option: T) => string,
  getOptionSearchText: (option: T) => string,
): VisibleOptions<T> {
  const needle = text.trim().toLocaleLowerCase();
  const pinnedKeys = new Set(pinnedOptions.map(getOptionKey));
  return {
    pinned: pinnedOptions.filter((option) =>
      getOptionSearchText(option).toLocaleLowerCase().includes(needle),
    ),
    results: searchResults.filter(
      (option) => !pinnedKeys.has(getOptionKey(option)),
    ),
  };
}
