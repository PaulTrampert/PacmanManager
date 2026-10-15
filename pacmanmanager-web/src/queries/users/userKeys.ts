import type { UserListSearch } from "./userListQuery";

/**
 * The query keys of the users resource. Every key starts with the resource name, so `userKeys.all`
 * matches all of them; lists and details sit under keys of their own, so a mutation can invalidate
 * every list without touching a detail, and the other way round.
 */
export const userKeys = {
  /** Matches every user query. */
  all: ["users"] as const,
  /** Matches every user list query, whatever its search. */
  lists: () => [...userKeys.all, "list"] as const,
  /** The list query for this search. */
  list: (search: UserListSearch) => [...userKeys.lists(), search] as const,
  /** The detail query of the user with this id. */
  detail: (id: string) => [...userKeys.all, "detail", id] as const,
};
