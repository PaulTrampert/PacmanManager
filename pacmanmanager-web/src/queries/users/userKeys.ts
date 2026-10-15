/**
 * The query keys of the users resource. Every key starts with the resource name, so `userKeys.all`
 * matches all of them; details sit under a key of their own, so invalidating the user lists never
 * refetches a detail.
 */
export const userKeys = {
  /** Matches every user query. */
  all: ["users"] as const,
  /** The detail query of the user with this id. */
  detail: (id: string) => [...userKeys.all, "detail", id] as const,
};
