import { useSuspenseQuery } from "@tanstack/react-query";
import { createFileRoute, linkOptions } from "@tanstack/react-router";
import { PageHeader } from "../components/PageHeader";
import { RepositoryForm } from "../components/RepositoryForm";
import { architecturesQuery } from "../queries/architectures";
import { requireUser } from "../session";

export const Route = createFileRoute("/repositories/new")({
  beforeLoad: requireUser,
  loader: ({ context: { queryClient } }) =>
    queryClient.ensureQueryData(architecturesQuery()),
  component: NewRepositoryPage,
});

// The form is connected to useCreateRepository, and Cancel to the list, by step 37 of the Web UI:
// Repositories plan; until then its callbacks do nothing.
function notYetConnected() {}

function NewRepositoryPage() {
  // Ensured by the loader, so it never suspends.
  const { data: architectures } = useSuspenseQuery(architecturesQuery());

  return (
    <>
      <PageHeader
        title="New repository"
        breadcrumbs={[
          {
            label: "Repositories",
            link: linkOptions({ to: "/repositories" }),
          },
          "New repository",
        ]}
      />
      <RepositoryForm
        architectures={architectures}
        onSubmit={notYetConnected}
        onCancel={notYetConnected}
      />
    </>
  );
}
