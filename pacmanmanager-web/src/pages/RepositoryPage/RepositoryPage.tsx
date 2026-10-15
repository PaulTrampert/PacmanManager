import type { LinkOptions } from "@tanstack/react-router";
import { PacmanConfSection } from "../../components/PacmanConfSection";
import { RepositoryHeader } from "../../components/RepositoryHeader";
import { useRepositoryPage } from "./useRepositoryPage";

/** The props {@link RepositoryPage} takes. */
export interface RepositoryPageProps {
  /** The id of the repository the page shows. */
  repositoryId: string;
  /** Where the **Repositories** breadcrumb links to: the repository list. */
  repositoriesLink: LinkOptions;
  /** Where **Edit** links to: the repository's edit page. */
  editLink: LinkOptions;
  /** Called when **Delete** is clicked. */
  onDelete: () => void;
}

/**
 * The repository page: the repository's header, with its breadcrumbs, actions and details, above
 * its collapsed `pacman.conf` section.
 */
export function RepositoryPage({
  repositoryId,
  repositoriesLink,
  editLink,
  onDelete,
}: RepositoryPageProps) {
  const { repository, architectures } = useRepositoryPage(repositoryId);
  return (
    <>
      <RepositoryHeader
        repository={repository}
        architectures={architectures}
        repositoriesLink={repositoriesLink}
        editLink={editLink}
        onDelete={onDelete}
      />
      <PacmanConfSection repository={repository} />
    </>
  );
}
