/**
 * The query keys of the architectures resource. Every key starts with the resource name, so
 * `architectureKeys.all` matches all of them.
 */
export const architectureKeys = {
  /** Matches every architecture query. */
  all: ["architectures"] as const,
  /** The query of the architectures a repository may support. */
  list: () => [...architectureKeys.all, "list"] as const,
};
