import {
  createFileRoute,
  linkOptions,
  useNavigate,
} from "@tanstack/react-router";
import { notFoundOn404 } from "../notFoundOn404";
import { EditRepositoryPage } from "../pages/EditRepositoryPage";
import { architecturesQuery } from "../queries/architectures";
import { repositoryQuery } from "../queries/repositories";
import { requireUser } from "../session";

/**
 * The edit repository page. Only a signed-in user may open it; one the API answers `404` for,
 * whether missing or hidden from the caller, is the not-found page.
 */
export const Route = createFileRoute("/repositories/$repositoryId/edit")({
  beforeLoad: requireUser,
  loader: ({ context: { queryClient }, params: { repositoryId } }) =>
    Promise.all([
      queryClient.ensureQueryData(repositoryQuery(repositoryId)),
      queryClient.ensureQueryData(architecturesQuery()),
    ]),
  onError: notFoundOn404,
  component: EditRepositoryRoute,
});

/** Where the **Repositories** crumb goes. */
const repositoriesLink = linkOptions({ to: "/repositories" });

function EditRepositoryRoute() {
  const { repositoryId } = Route.useParams();
  const navigate = useNavigate();
  const repositoryLink = linkOptions({
    to: "/repositories/$repositoryId",
    params: { repositoryId },
  });
  // Saving and cancelling both return to the repository's page.
  const toRepository = () => void navigate(repositoryLink);
  return (
    <EditRepositoryPage
      repositoryId={repositoryId}
      repositoriesLink={repositoriesLink}
      repositoryLink={repositoryLink}
      onSaved={toRepository}
      onCancel={toRepository}
    />
  );
}
