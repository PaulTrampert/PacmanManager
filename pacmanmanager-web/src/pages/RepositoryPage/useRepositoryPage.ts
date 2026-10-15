import { useSuspenseQuery } from "@tanstack/react-query";
import { architecturesQuery } from "../../queries/architectures";
import { repositoryQuery } from "../../queries/repositories";

/**
 * What {@link RepositoryPage} shows: the repository and the architectures its own are named by,
 * read with the factories the route's loader ensured, so neither suspends once the route has
 * loaded.
 */
export function useRepositoryPage(repositoryId: string) {
  const { data: repository } = useSuspenseQuery(repositoryQuery(repositoryId));
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  return { repository, architectures };
}
