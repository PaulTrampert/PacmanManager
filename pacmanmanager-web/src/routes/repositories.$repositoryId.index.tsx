import {
  createFileRoute,
  linkOptions,
  useNavigate,
} from "@tanstack/react-router";
import { notFoundOn404 } from "../notFoundOn404";
import { RepositoryPage } from "../pages/RepositoryPage";
import { architecturesQuery } from "../queries/architectures";
import { repositoryQuery } from "../queries/repositories";

/**
 * The repository page. Anyone who can see the repository may open it; one the API answers `404`
 * for, whether missing or hidden from the caller, is the not-found page.
 */
export const Route = createFileRoute("/repositories/$repositoryId/")({
  loader: ({ context: { queryClient }, params: { repositoryId } }) =>
    Promise.all([
      queryClient.ensureQueryData(repositoryQuery(repositoryId)),
      queryClient.ensureQueryData(architecturesQuery()),
    ]),
  onError: notFoundOn404,
  component: RepositoryRoute,
});

/** Where the **Repositories** breadcrumb goes. */
const repositoriesLink = linkOptions({ to: "/repositories" });

function RepositoryRoute() {
  const { repositoryId } = Route.useParams();
  const navigate = useNavigate();
  return (
    <RepositoryPage
      repositoryId={repositoryId}
      repositoriesLink={repositoriesLink}
      editLink={linkOptions({
        to: "/repositories/$repositoryId/edit",
        params: { repositoryId },
      })}
      onDeleted={() => navigate(repositoriesLink)}
    />
  );
}
