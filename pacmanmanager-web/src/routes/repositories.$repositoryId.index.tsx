import { useSuspenseQuery } from "@tanstack/react-query";
import { createFileRoute, linkOptions } from "@tanstack/react-router";
import { PacmanConfSection } from "../components/PacmanConfSection";
import { RepositoryHeader } from "../components/RepositoryHeader";
import { notFoundOn404 } from "../notFoundOn404";
import { architecturesQuery } from "../queries/architectures";
import { repositoryQuery } from "../queries/repositories";

/**
 * The repository page: the repository's header, with its breadcrumbs, actions and details, above
 * its collapsed `pacman.conf` section. Anyone who can see the repository may open it; one the API
 * answers `404` for, whether missing or hidden from the caller, is the not-found page.
 */
export const Route = createFileRoute("/repositories/$repositoryId/")({
  loader: ({ context: { queryClient }, params: { repositoryId } }) =>
    Promise.all([
      queryClient.ensureQueryData(repositoryQuery(repositoryId)),
      queryClient.ensureQueryData(architecturesQuery()),
    ]),
  onError: notFoundOn404,
  component: RepositoryPage,
});

/** Where the **Repositories** breadcrumb goes. */
const repositoriesLink = linkOptions({ to: "/repositories" });

function RepositoryPage() {
  const { repositoryId } = Route.useParams();
  const { data: repository } = useSuspenseQuery(repositoryQuery(repositoryId));
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  const editLink = linkOptions({
    to: "/repositories/$repositoryId/edit",
    params: { repositoryId },
  });

  return (
    <>
      <RepositoryHeader
        repository={repository}
        architectures={architectures}
        repositoriesLink={repositoriesLink}
        editLink={editLink}
        // Opening the delete dialog is step 39 of the Web UI: Repositories plan.
        onDelete={() => {}}
      />
      <PacmanConfSection repository={repository} />
    </>
  );
}
