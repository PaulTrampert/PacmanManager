import type { RepositoryListSearch } from "./repositoryListQuery";

/**
 * The query keys of the repositories resource. Every key starts with the resource name, so
 * `repositoryKeys.all` matches all of them; lists and details sit under keys of their own, so a
 * mutation can invalidate every list without touching a detail, and the other way round.
 */
export const repositoryKeys = {
  /** Matches every repository query. */
  all: ["repositories"] as const,
  /** Matches every repository list query, whatever its search. */
  lists: () => [...repositoryKeys.all, "list"] as const,
  /** The list query for this search. */
  list: (search: RepositoryListSearch) =>
    [...repositoryKeys.lists(), search] as const,
  /** The detail query of the repository with this id. */
  detail: (id: string) => [...repositoryKeys.all, "detail", id] as const,
};
