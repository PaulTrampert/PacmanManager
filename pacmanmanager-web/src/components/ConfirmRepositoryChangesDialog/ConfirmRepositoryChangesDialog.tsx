import DialogContentText from "@mui/material/DialogContentText";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import type { Repository } from "../../api";
import { Dialog } from "../Dialog";
import {
  repositoryChanges,
  type EditedRepository,
  type RepositoryChange,
} from "./repositoryChanges";

/** The props {@link ConfirmRepositoryChangesDialog} takes. */
export interface ConfirmRepositoryChangesDialogProps {
  /** Whether the dialog is shown. */
  open: boolean;
  /** The repository as the API last returned it. */
  repository: Repository;
  /** The name and visibility the edit form is about to save it with. */
  edited: EditedRepository;
  /** Called when **Save** is chosen, to send the update. */
  onConfirm: () => void;
  /** Called when **Cancel** is chosen, or the dialog is dismissed, to return to the form. */
  onCancel: () => void;
  /** Whether the update is in flight; the dialog then stays open with its actions disabled. */
  pending?: boolean;
}

/** What a change does to the clients already using the repository, as the dialog says it. */
function consequence(change: RepositoryChange, edited: EditedRepository) {
  switch (change) {
    case "rename":
      return `Renaming it to ${edited.name}: every pacman.conf using this repository must change its section name to [${edited.name}], or pacman will stop finding it.`;
    case "makePublic":
      return "Making it public: anyone, signed in or not, will be able to read every package in this repository.";
    case "makePrivate":
      return "Making it private: clients that read this repository without credentials will lose access.";
  }
}

/**
 * The **Save these changes?** dialog the edit form opens before saving. It lists what each change
 * from the stored repository to the edited one does to the clients already using it: a rename,
 * then a change of visibility.
 */
export function ConfirmRepositoryChangesDialog({
  open,
  repository,
  edited,
  onConfirm,
  onCancel,
  pending = false,
}: ConfirmRepositoryChangesDialogProps) {
  return (
    <Dialog
      open={open}
      title="Save these changes?"
      confirmLabel="Save"
      onConfirm={onConfirm}
      onCancel={onCancel}
      pending={pending}
    >
      <DialogContentText color="textPrimary">
        Clients already using {repository.name} will be affected:
      </DialogContentText>
      <List
        aria-label="Consequences"
        sx={{ listStyleType: "disc", pl: 2.5, pb: 0 }}
      >
        {repositoryChanges(repository, edited).map((change) => (
          <ListItem key={change} sx={{ display: "list-item", px: 0 }}>
            <ListItemText
              secondary={consequence(change, edited)}
              slotProps={{ secondary: { variant: "body1" } }}
            />
          </ListItem>
        ))}
      </List>
    </Dialog>
  );
}
