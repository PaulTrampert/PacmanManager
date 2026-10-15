import Box from "@mui/material/Box";
import type { LinkOptions } from "@tanstack/react-router";
import { DeleteRepositoryDialog } from "../../components/DeleteRepositoryDialog";
import { PacmanConfSection } from "../../components/PacmanConfSection";
import { ProblemDetailsAlert } from "../../components/ProblemDetailsAlert";
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
  /**
   * Called once the repository has been deleted: navigating to the repository list. The delete
   * dialog stays pending until it settles.
   */
  onDeleted: () => Promise<unknown>;
}

/**
 * The repository page: the repository's header, with its breadcrumbs, actions and details, above
 * its collapsed `pacman.conf` section. **Delete** opens the delete dialog; a delete that fails
 * closes it and is shown in an alert above the header.
 */
export function RepositoryPage({
  repositoryId,
  repositoriesLink,
  editLink,
  onDeleted,
}: RepositoryPageProps) {
  const page = useRepositoryPage(repositoryId, onDeleted);
  return (
    <>
      {page.deleteError != null && (
        <Box sx={{ mb: 3 }}>
          <ProblemDetailsAlert
            error={page.deleteError}
            title={`${page.repository.name ?? ""} was not deleted`}
            onClose={page.onDeleteErrorDismiss}
          />
        </Box>
      )}
      <RepositoryHeader
        repository={page.repository}
        architectures={page.architectures}
        repositoriesLink={repositoriesLink}
        editLink={editLink}
        onDelete={page.onDelete}
      />
      <PacmanConfSection repository={page.repository} />
      <DeleteRepositoryDialog
        open={page.deleteDialogOpen}
        repository={page.repository}
        onConfirm={page.onDeleteConfirm}
        onCancel={page.onDeleteCancel}
        pending={page.deletePending}
      />
    </>
  );
}
