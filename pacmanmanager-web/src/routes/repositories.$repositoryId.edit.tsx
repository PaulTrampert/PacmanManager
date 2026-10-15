import { useSuspenseQuery } from "@tanstack/react-query";
import { createFileRoute, linkOptions } from "@tanstack/react-router";
import { ForbiddenMessage } from "../components/ForbiddenMessage";
import { PageHeader } from "../components/PageHeader";
import { RepositoryForm } from "../components/RepositoryForm";
import { notFoundOn404 } from "../notFoundOn404";
import { architecturesQuery } from "../queries/architectures";
import { repositoryQuery } from "../queries/repositories";
import { requireUser } from "../session";

/**
 * The edit repository page: `RepositoryForm` given the repository, which puts it in its edit
 * behaviour, or the `403` message in its place when the caller may not update it. Only a signed-in
 * user may open it; one the API answers `404` for, whether missing or hidden from the caller, is the
 * not-found page.
 */
export const Route = createFileRoute("/repositories/$repositoryId/edit")({
  beforeLoad: requireUser,
  loader: ({ context: { queryClient }, params: { repositoryId } }) =>
    Promise.all([
      queryClient.ensureQueryData(repositoryQuery(repositoryId)),
      queryClient.ensureQueryData(architecturesQuery()),
    ]),
  onError: notFoundOn404,
  component: EditRepositoryPage,
});

/** Where the **Repositories** breadcrumb goes. */
const repositoriesLink = linkOptions({ to: "/repositories" });

// Saving, after confirmation, and Cancel are connected by step 38 of the Web UI: Repositories plan;
// until then the form's callbacks do nothing.
function notYetConnected() {}

function EditRepositoryPage() {
  const { repositoryId } = Route.useParams();
  // Both are ensured by the loader, so neither suspends.
  const { data: repository } = useSuspenseQuery(repositoryQuery(repositoryId));
  const { data: architectures } = useSuspenseQuery(architecturesQuery());

  return (
    <>
      <PageHeader
        title="Edit repository"
        breadcrumbs={[
          { label: "Repositories", link: repositoriesLink },
          {
            label: repository.name ?? "",
            link: linkOptions({
              to: "/repositories/$repositoryId",
              params: { repositoryId },
            }),
          },
          "Edit",
        ]}
      />
      {repository.permissions.update ? (
        <RepositoryForm
          repository={repository}
          architectures={architectures}
          onSubmit={notYetConnected}
          onCancel={notYetConnected}
        />
      ) : (
        <ForbiddenMessage />
      )}
    </>
  );
}
