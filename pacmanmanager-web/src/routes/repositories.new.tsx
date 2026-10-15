import {
  createFileRoute,
  linkOptions,
  useNavigate,
} from "@tanstack/react-router";
import { NewRepositoryPage } from "../pages/NewRepositoryPage";
import { architecturesQuery } from "../queries/architectures";
import { requireUser } from "../session";

export const Route = createFileRoute("/repositories/new")({
  beforeLoad: requireUser,
  loader: ({ context: { queryClient } }) =>
    queryClient.ensureQueryData(architecturesQuery()),
  component: NewRepositoryRoute,
});

function NewRepositoryRoute() {
  const navigate = useNavigate();
  return (
    <NewRepositoryPage
      repositoriesLink={linkOptions({ to: "/repositories" })}
      onCreated={(repository) =>
        void navigate({
          to: "/repositories/$repositoryId",
          params: { repositoryId: repository.id },
        })
      }
      onCancel={() => void navigate({ to: "/repositories" })}
    />
  );
}
