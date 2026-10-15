import { useSuspenseQuery } from "@tanstack/react-query";
import { useState } from "react";
import type { RepositoryFormValues } from "../../components/RepositoryForm";
import { architecturesQuery } from "../../queries/architectures";
import { repositoryQuery } from "../../queries/repositories";
import { useUpdateRepository } from "../../queries/repositories/useUpdateRepository";

/** What {@link useEditRepositoryPage} needs from the page's props. */
export interface UseEditRepositoryPageOptions {
  /** The id of the repository to edit. */
  repositoryId: string;
  /** Called once the repository has been saved. */
  onSaved: () => void;
}

/**
 * The edit repository page's behaviour: it reads the repository and the architectures with the
 * factories its route's loader ensured, so neither suspends there, and says whether the caller may
 * update the repository.
 *
 * Submitting the form opens the confirmation dialog with the edited values; confirming sends the
 * update and calls `onSaved` once it succeeds. Any failure closes the dialog and hands the error to
 * the form, which still holds the edits.
 */
export function useEditRepositoryPage({
  repositoryId,
  onSaved,
}: UseEditRepositoryPageOptions) {
  const { data: repository } = useSuspenseQuery(repositoryQuery(repositoryId));
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  const update = useUpdateRepository();
  // Set by the first submit, and kept after the dialog closes so that its content does not change
  // while it fades out.
  const [edited, setEdited] = useState<RepositoryFormValues>();
  const [confirming, setConfirming] = useState(false);

  return {
    repository,
    architectures,
    /** Whether the form is shown; when not, the `403` message takes its place. */
    canUpdate: repository.permissions.update,
    /** The form's submit: opens the confirmation dialog for `values`. */
    onSubmit: (values: RepositoryFormValues) => {
      setEdited(values);
      setConfirming(true);
    },
    /** Whether the confirmation dialog is open. */
    confirming,
    /** The values the dialog confirms saving: the stored ones until the form is first submitted. */
    edited: edited ?? repository,
    /** The dialog's **Save**: sends the update, then calls `onSaved`, or closes the dialog on failure. */
    onConfirm: () => {
      if (edited === undefined) {
        return;
      }
      update.mutate(
        { id: repositoryId, request: edited },
        {
          onSuccess: onSaved,
          onError: () => setConfirming(false),
        },
      );
    },
    /** The dialog's **Cancel**: returns to the form with the edits intact. */
    onCancelConfirm: () => setConfirming(false),
    /** Whether the update is in flight. */
    pending: update.isPending,
    /** The last update's error, for the form to show. */
    error: update.error,
  };
}
