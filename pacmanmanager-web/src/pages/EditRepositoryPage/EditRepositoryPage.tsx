import type { LinkOptions } from "@tanstack/react-router";
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
  /** Called once the repository has been saved. Step 38 of the Web UI: Repositories plan calls it. */
  onSaved: () => void;
  /** Called when the form is cancelled. Step 38 of the Web UI: Repositories plan calls it. */
  onCancel: () => void;
}

// Saving, after confirmation, and Cancel are connected by step 38 of the Web UI: Repositories plan;
// until then the form's callbacks do nothing.
function notYetConnected() {}

/**
 * The edit repository page: breadcrumbs, the heading **Edit repository**, and `RepositoryForm`
 * given the repository, which puts it in its edit behaviour, or the `403` message in the form's
 * place when the caller may not update it.
 */
export function EditRepositoryPage({
  repositoryId,
  repositoriesLink,
  repositoryLink,
}: EditRepositoryPageProps) {
  const page = useEditRepositoryPage(repositoryId);
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
        <RepositoryForm
          repository={page.repository}
          architectures={page.architectures}
          onSubmit={notYetConnected}
          onCancel={notYetConnected}
        />
      ) : (
        <ForbiddenMessage />
      )}
    </>
  );
}
