import { useSuspenseQuery } from "@tanstack/react-query";
import { useState } from "react";
import type { Architecture, Repository } from "../../api";
import { architecturesQuery } from "../../queries/architectures";
import {
  repositoryQuery,
  useDeleteRepository,
} from "../../queries/repositories";

/** What {@link useRepositoryPage} gives the page to render. */
export interface RepositoryPageState {
  /** The repository the page shows. */
  repository: Repository;
  /** Every architecture a repository may support, by which the repository's are named. */
  architectures: readonly Architecture[];
  /** Whether the delete dialog is open. */
  deleteDialogOpen: boolean;
  /** Whether the delete request, or the navigation after it, is in flight. */
  deletePending: boolean;
  /** Why the last delete failed, until the alert is dismissed or the delete is tried again. */
  deleteError: unknown;
  /** Opens the delete dialog; for the header's **Delete**. */
  onDelete: () => void;
  /** Deletes the repository; for the dialog's **Delete**. A failure closes the dialog. */
  onDeleteConfirm: () => void;
  /** Closes the delete dialog without deleting. */
  onDeleteCancel: () => void;
  /** Dismisses the alert saying the repository was not deleted. */
  onDeleteErrorDismiss: () => void;
}

/**
 * The repository page's behaviour. It reads the repository and the architectures with the
 * factories the route's loader ensured, so neither suspends once the route has loaded, and runs the
 * delete dialog: confirming deletes the repository and then calls `onDeleted`, which navigates away
 * before the repository's detail query is removed; a failure closes the dialog and leaves the error
 * for the page to show above its header.
 *
 * @param repositoryId the id of the repository the page shows.
 * @param onDeleted called once the repository is deleted; the delete stays pending until it settles.
 */
export function useRepositoryPage(
  repositoryId: string,
  onDeleted: () => Promise<unknown>,
): RepositoryPageState {
  const { data: repository } = useSuspenseQuery(repositoryQuery(repositoryId));
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const deletion = useDeleteRepository({ onDeleted });

  return {
    repository,
    architectures,
    deleteDialogOpen,
    deletePending: deletion.isPending,
    deleteError: deletion.error,
    onDelete: () => {
      setDeleteDialogOpen(true);
    },
    onDeleteConfirm: () => {
      deletion.mutate(repositoryId, {
        onError: () => {
          setDeleteDialogOpen(false);
        },
      });
    },
    onDeleteCancel: () => {
      setDeleteDialogOpen(false);
    },
    onDeleteErrorDismiss: () => {
      deletion.reset();
    },
  };
}
