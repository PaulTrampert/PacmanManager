/**
 * `selected` reordered to follow `options`: the values among the options in the options' order,
 * then any that are not, in the order they were given. Nothing is added, dropped or duplicated.
 */
export function inOptionOrder(
  selected: readonly string[],
  options: readonly { value: string }[],
): string[] {
  const optionValues = options.map((option) => option.value);
  const chosen = new Set(selected);
  const known = new Set(optionValues);
  return [
    ...optionValues.filter((value) => chosen.has(value)),
    ...selected.filter((value) => !known.has(value)),
  ];
}
