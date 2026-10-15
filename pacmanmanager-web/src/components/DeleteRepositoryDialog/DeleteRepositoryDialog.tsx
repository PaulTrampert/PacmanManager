import DialogContentText from "@mui/material/DialogContentText";
import TextField from "@mui/material/TextField";
import type { Repository } from "../../api";
import { Dialog } from "../Dialog";
import { useDeleteRepositoryDialog } from "./useDeleteRepositoryDialog";

/** The props {@link DeleteRepositoryDialog} takes. */
export interface DeleteRepositoryDialogProps {
  /** Whether the dialog is shown. */
  open: boolean;
  /** The repository the dialog deletes. */
  repository: Repository;
  /** Called when **Delete** is chosen, which it can be only once the name is typed exactly. */
  onConfirm: () => void;
  /** Called when **Cancel** is chosen, or the dialog is dismissed. */
  onCancel: () => void;
  /**
   * Whether the delete request is in flight. While it is, the dialog stays open with its actions
   * disabled.
   */
  pending?: boolean;
}

/**
 * The dialog that confirms deleting a repository: **Delete** is enabled only once the
 * repository's name has been typed exactly, case included. It shows no errors itself: on failure
 * the page closes it and shows the error behind it.
 */
export function DeleteRepositoryDialog({
  open,
  repository,
  onConfirm,
  onCancel,
  pending = false,
}: DeleteRepositoryDialogProps) {
  const dialog = useDeleteRepositoryDialog(repository.name, open);
  const name = repository.name ?? "";
  return (
    <Dialog
      open={open}
      title={`Delete ${name}?`}
      confirmLabel="Delete"
      confirmColor="error"
      confirmDisabled={!dialog.confirmed}
      onConfirm={onConfirm}
      onCancel={onCancel}
      pending={pending}
    >
      <DialogContentText>
        This permanently deletes {name} and every package in it. Clients using
        it will fail. This can&apos;t be undone.
      </DialogContentText>
      <TextField
        label={`Type ${name} to confirm`}
        value={dialog.typed}
        onChange={dialog.onTypedChange}
        autoFocus
        fullWidth
        margin="normal"
        autoComplete="off"
        slotProps={{ htmlInput: { spellCheck: false } }}
      />
    </Dialog>
  );
}
