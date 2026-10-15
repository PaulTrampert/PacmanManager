import type { Architecture, RepositorySortField, SortDirection } from "./api";
import type { RepositoryListSearch } from "./queries/repositories";
import type { UserListSearch } from "./queries/users";
import type { Session } from "./session";

/** The page sizes the repository list offers. The API accepts more; the web app sends only these. */
export const repositoryListPageSizes = [25, 50, 100] as const;

/** A page size the repository list offers. */
export type RepositoryListPageSize = (typeof repositoryListPageSizes)[number];

/** The page size the repository list uses when its URL names none. */
export const defaultRepositoryListPageSize: RepositoryListPageSize = 50;

/**
 * The repository list's search params: those of `GET /v1/Repositories`, under the API's names, with
 * `pageSize` narrowed to {@link repositoryListPageSizes}. Every one is optional.
 */
export type RepositoryListPageSearch = Omit<
  RepositoryListSearch,
  "pageSize"
> & {
  pageSize?: RepositoryListPageSize;
};

/** The filter params among {@link RepositoryListPageSearch}'s. */
export const repositoryListFilterParams = [
  "nameContains",
  "isPublic",
  "ownerId",
  "architecture",
] as const satisfies readonly (keyof RepositoryListPageSearch)[];

/**
 * The users search that counts the users, by its `total`: the owner filter is disabled when there
 * are fewer than two.
 */
export const userCountSearch: UserListSearch = { pageSize: 1 };

// Records rather than arrays, so that a value the API adds fails the typecheck until it is listed.
const sortFields: Record<RepositorySortField, true> = {
  Name: true,
  Created: true,
  Updated: true,
};
const sortDirections: Record<SortDirection, true> = {
  Ascending: true,
  Descending: true,
};

function isOneOf<T extends string>(
  values: Record<T, true>,
  value: unknown,
): value is T {
  return typeof value === "string" && Object.hasOwn(values, value);
}

/**
 * A search param as text. The router parses each param as JSON where it can, so a name typed as
 * `123` arrives as a number; it is still the text that was typed.
 */
function asText(value: unknown): string | undefined {
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }
  return typeof value === "string" && value !== "" ? value : undefined;
}

/**
 * The repository list route's `validateSearch`. Each param is kept only when it is one the API
 * would accept; anything else, including a `pageSize` other than 25, 50 or 100, is dropped, so the
 * page uses its default. The route's `beforeLoad` then removes a dropped param from the URL, so the
 * URL never disagrees with what the page shows.
 */
export function validateRepositoryListSearch(
  raw: Record<string, unknown>,
): RepositoryListPageSearch {
  const search: RepositoryListPageSearch = {};
  const nameContains = asText(raw.nameContains);
  if (nameContains !== undefined) {
    search.nameContains = nameContains;
  }
  if (typeof raw.isPublic === "boolean") {
    search.isPublic = raw.isPublic;
  }
  const ownerId = asText(raw.ownerId);
  if (ownerId !== undefined) {
    search.ownerId = ownerId;
  }
  const architecture = asText(raw.architecture);
  if (architecture !== undefined) {
    search.architecture = architecture;
  }
  if (isOneOf(sortFields, raw.sortBy)) {
    search.sortBy = raw.sortBy;
  }
  if (isOneOf(sortDirections, raw.direction)) {
    search.direction = raw.direction;
  }
  if (
    typeof raw.offset === "number" &&
    Number.isInteger(raw.offset) &&
    raw.offset >= 0
  ) {
    search.offset = raw.offset;
  }
  const pageSize = repositoryListPageSizes.find(
    (size) => size === raw.pageSize,
  );
  if (pageSize !== undefined) {
    search.pageSize = pageSize;
  }
  return search;
}

/**
 * Whether `given`, the search params the route was given, holds any that
 * {@link validateRepositoryListSearch} dropped from `search`, so that the URL disagrees with the
 * page.
 */
export function hasDroppedParams(
  given: Record<string, unknown>,
  search: RepositoryListPageSearch,
): boolean {
  return Object.keys(given).some(
    (param) =>
      given[param] !== undefined &&
      search[param as keyof RepositoryListPageSearch] === undefined,
  );
}

/** What decides which of the repository list's filters are disabled. */
export interface RepositoryListFilterState {
  /** Who is using the app: an anonymous visitor's visibility filter is disabled. */
  session: Session;
  /** The architectures a repository may support: with fewer than two, that filter is disabled. */
  architectures: readonly Architecture[];
  /** How many users there are: with fewer than two, the owner filter is disabled. */
  userCount: number;
}

/**
 * `search` without the params of the filters that are disabled: `architecture` with fewer than two
 * architectures, `ownerId` with fewer than two users, and `isPublic` for an anonymous visitor. It is
 * `search` itself when none of those is present, so the caller can tell whether anything was removed.
 */
export function withoutDisabledFilters(
  search: RepositoryListPageSearch,
  { session, architectures, userCount }: RepositoryListFilterState,
): RepositoryListPageSearch {
  const disabled: (keyof RepositoryListPageSearch)[] = [];
  if (architectures.length < 2) {
    disabled.push("architecture");
  }
  if (userCount < 2) {
    disabled.push("ownerId");
  }
  if (session.status === "anonymous") {
    disabled.push("isPublic");
  }
  const present = disabled.filter((param) => search[param] !== undefined);
  if (present.length === 0) {
    return search;
  }
  const remaining = { ...search };
  for (const param of present) {
    delete remaining[param];
  }
  return remaining;
}

/** Whether any filter is applied: a filter param of `search` is present. */
export function hasRepositoryListFilters(
  search: RepositoryListPageSearch,
): boolean {
  return repositoryListFilterParams.some(
    (param) => search[param] !== undefined,
  );
}

/**
 * The list request for `search`: its params as they are, with the page size defaulted to
 * {@link defaultRepositoryListPageSize}, which is the web app's choice rather than the API's.
 */
export function repositoryListRequest(
  search: RepositoryListPageSearch,
): RepositoryListSearch {
  return {
    ...search,
    pageSize: search.pageSize ?? defaultRepositoryListPageSize,
  };
}
