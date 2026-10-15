import type { LinkOptions } from "@tanstack/react-router";
import type { Repository } from "../../api";
import { PageHeader } from "../../components/PageHeader";
import { RepositoryForm } from "../../components/RepositoryForm";
import { useNewRepositoryPage } from "./useNewRepositoryPage";

/** The props {@link NewRepositoryPage} takes. */
export interface NewRepositoryPageProps {
  /** Where the **Repositories** crumb links to: the repository list. */
  repositoriesLink: LinkOptions;
  /** Called with the repository once it has been created, to show it. */
  onCreated: (repository: Repository) => void;
  /** Called when **Cancel** is chosen, to leave the page without creating anything. */
  onCancel: () => void;
}

// The form is connected to useCreateRepository, onCreated and onCancel by step 37 of the Web UI:
// Repositories plan; until then its callbacks do nothing.
function notYetConnected() {}

/** The page for creating a repository: its breadcrumbs, its heading and an empty `RepositoryForm`. */
export function NewRepositoryPage({
  repositoriesLink,
}: NewRepositoryPageProps) {
  const page = useNewRepositoryPage();
  return (
    <>
      <PageHeader
        title="New repository"
        breadcrumbs={[
          { label: "Repositories", link: repositoriesLink },
          "New repository",
        ]}
      />
      <RepositoryForm
        architectures={page.architectures}
        onSubmit={notYetConnected}
        onCancel={notYetConnected}
      />
    </>
  );
}
