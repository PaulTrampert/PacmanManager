import type { LinkOptions } from "@tanstack/react-router";
import { ConfirmRepositoryChangesDialog } from "../../components/ConfirmRepositoryChangesDialog";
import { ForbiddenMessage } from "../../components/ForbiddenMessage";
import { PageHeader } from "../../components/PageHeader";
import { RepositoryForm } from "../../components/RepositoryForm";
import { useEditRepositoryPage } from "./useEditRepositoryPage";

/** The props {@link EditRepositoryPage} takes. */
export interface EditRepositoryPageProps {
  /** The id of the repository to edit. */
  repositoryId: string;
  /** Where the **Repositories** crumb goes. */
  repositoriesLink: LinkOptions;
  /** Where the crumb naming the repository goes: its page. */
  repositoryLink: LinkOptions;
  /** Called once the repository has been saved: the route returns to the repository's page. */
  onSaved: () => void;
  /** Called when the form is cancelled, without saving: the route returns to the repository's page. */
  onCancel: () => void;
}

/**
 * The edit repository page: breadcrumbs, the heading **Edit repository**, and `RepositoryForm`
 * given the repository, which puts it in its edit behaviour, or the `403` message in the form's
 * place when the caller may not update it. **Save** opens the **Save these changes?** dialog, whose
 * **Save** sends the update; a failure closes it and shows in the form, with the edits intact.
 */
export function EditRepositoryPage({
  repositoryId,
  repositoriesLink,
  repositoryLink,
  onSaved,
  onCancel,
}: EditRepositoryPageProps) {
  const page = useEditRepositoryPage({ repositoryId, onSaved });
  return (
    <>
      <PageHeader
        title="Edit repository"
        breadcrumbs={[
          { label: "Repositories", link: repositoriesLink },
          { label: page.repository.name ?? "", link: repositoryLink },
          "Edit",
        ]}
      />
      {page.canUpdate ? (
        <>
          <RepositoryForm
            repository={page.repository}
            architectures={page.architectures}
            onSubmit={page.onSubmit}
            onCancel={onCancel}
            pending={page.pending}
            error={page.error}
          />
          <ConfirmRepositoryChangesDialog
            open={page.confirming}
            repository={page.repository}
            edited={page.edited}
            onConfirm={page.onConfirm}
            onCancel={page.onCancelConfirm}
            pending={page.pending}
          />
        </>
      ) : (
        <ForbiddenMessage />
      )}
    </>
  );
}
