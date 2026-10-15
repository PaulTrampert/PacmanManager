import type { Repository } from "../../api";

/**
 * A change to a repository that affects the clients already using it:
 *
 * * `rename` — its name changed;
 * * `makePublic` — it went from private to public;
 * * `makePrivate` — it went from public to private.
 */
export type RepositoryChange = "rename" | "makePublic" | "makePrivate";

/**
 * The fields of an edited repository that {@link repositoryChanges} compares. Both a `Repository`
 * and the `WriteRepositoryRequest` the edit form saves have them.
 */
export type EditedRepository = Pick<Repository, "name" | "isPublic">;

/**
 * The changes from `stored` to `edited` that affect clients already using the repository, in the
 * order the confirmation lists them: a rename first, then a change of visibility. A repository
 * that leaves `isPublic` out is private, as the API treats it.
 *
 * @param stored the repository as the API last returned it.
 * @param edited the name and visibility it is about to be saved with.
 */
export function repositoryChanges(
  stored: Repository,
  edited: EditedRepository,
): RepositoryChange[] {
  const changes: RepositoryChange[] = [];
  if (edited.name !== stored.name) {
    changes.push("rename");
  }
  const isPublic = edited.isPublic ?? false;
  if (isPublic !== (stored.isPublic ?? false)) {
    changes.push(isPublic ? "makePublic" : "makePrivate");
  }
  return changes;
}
