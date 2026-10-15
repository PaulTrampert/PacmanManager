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

/**
 * The page for creating a repository: its breadcrumbs, its heading and an empty `RepositoryForm`,
 * which creates the repository it submits and hands it to `onCreated`.
 */
export function NewRepositoryPage({
  repositoriesLink,
  onCreated,
  onCancel,
}: NewRepositoryPageProps) {
  const page = useNewRepositoryPage(onCreated);
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
        onSubmit={page.create}
        onCancel={onCancel}
        pending={page.pending}
        error={page.error}
      />
    </>
  );
}
