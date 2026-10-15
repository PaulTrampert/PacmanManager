import type { Architecture } from "./api";

/**
 * The names to show for a repository's supported architectures, in the order the repository lists
 * them: each one's `displayValue` from `architectures`, or the raw value for one not in it.
 */
export function displayArchitectures(
  supported: readonly string[],
  architectures: readonly Architecture[],
): string[] {
  return supported.map(
    (value) =>
      architectures.find((architecture) => architecture.value === value)
        ?.displayValue ?? value,
  );
}
