import { useSuspenseQuery } from "@tanstack/react-query";
import { architecturesQuery } from "../../queries/architectures";
import { repositoryQuery } from "../../queries/repositories";

/**
 * The edit repository page's behaviour: it reads the repository and the architectures with the
 * factories its route's loader ensured, so neither suspends there, and says whether the caller may
 * update the repository.
 */
export function useEditRepositoryPage(repositoryId: string) {
  const { data: repository } = useSuspenseQuery(repositoryQuery(repositoryId));
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  return {
    repository,
    architectures,
    /** Whether the form is shown; when not, the `403` message takes its place. */
    canUpdate: repository.permissions.update,
  };
}
