// Builders for the values the API returns, for stories and tests to answer requests with. Each is
// typed from the generated schema and fills every required field with a valid default, so a field
// the API adds is filled here and nowhere else. Stories and tests build API values only through
// these, never as literal objects.
import type {
  Architecture,
  CurrentUser,
  PublicUserInfo,
  PublicUserInfoPaginatedResponse,
  Repository,
  RepositoryPaginatedResponse,
} from "../api";

/** A signed-in user's own record, with any field overridden. */
export function aUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: "6f1c1f0e-8a52-4a3b-9a57-2c5d3e8f9b10",
    displayName: "Test User",
    email: "test@example.com",
    ...overrides,
  };
}

/**
 * A repository, with any field overridden. By default it is public, owned by {@link aUser}'s
 * default user, and its caller may update and delete it.
 */
export function aRepository(overrides: Partial<Repository> = {}): Repository {
  const { id, displayName } = aUser();
  return {
    id: "0b6c3d2e-4f5a-4b7c-8d9e-1f2a3b4c5d6e",
    name: "my-repo",
    supportedArchitectures: ["x86_64"],
    isPublic: true,
    owner: { id, displayName },
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    permissions: { update: true, delete: true },
    serverUrl: "http://localhost:8082/pacman/$repo/$arch",
    ...overrides,
  };
}

/** An architecture a repository may support, with any field overridden. */
export function anArchitecture(
  overrides: Partial<Architecture> = {},
): Architecture {
  return {
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
    ...overrides,
  };
}

/** The items {@link aPage} makes a page of. */
export type PageItem = Repository | PublicUserInfo;

/** The paginated response the API lists items of type `T` in. */
export type PageOf<T extends PageItem> = [T] extends [Repository]
  ? RepositoryPaginatedResponse
  : PublicUserInfoPaginatedResponse;

/** What {@link aPage} may be told besides its results. */
export interface PageOptions<T extends PageItem> {
  /** The sort the page reports as applied. Defaults to the API's default for the listing. */
  sort?: PageOf<T>["sort"];
  /** The position of the page's first item in the whole listing. Defaults to `0`. */
  offset?: number;
  /** The number of items in the whole listing. Defaults to `offset` plus the page's items. */
  total?: number;
}

const repositoryDefaultSort: RepositoryPaginatedResponse["sort"] = {
  sortBy: "Name",
  direction: "Ascending",
};

const userDefaultSort: PublicUserInfoPaginatedResponse["sort"] = {
  sortBy: "DisplayName",
  direction: "Ascending",
};

/**
 * One page of a listing holding `results`. Its sort defaults to the API's default for the listing
 * the results come from, which is told from the first result; a page with no results cannot be
 * told apart, so it must be given its `sort`.
 */
export function aPage<T extends PageItem>(
  results: readonly [T, ...T[]],
  options?: PageOptions<T>,
): PageOf<T>;
export function aPage<T extends PageItem>(
  results: readonly T[],
  options: PageOptions<T> & Required<Pick<PageOptions<T>, "sort">>,
): PageOf<T>;
export function aPage<T extends PageItem>(
  results: readonly T[],
  { sort, offset = 0, total }: PageOptions<T> = {},
): PageOf<T> {
  const first = results[0];
  const defaultSort =
    first !== undefined && "serverUrl" in first
      ? repositoryDefaultSort
      : userDefaultSort;
  return {
    results: [...results],
    offset,
    total: total ?? offset + results.length,
    sort: sort ?? defaultSort,
  } as PageOf<T>;
}
