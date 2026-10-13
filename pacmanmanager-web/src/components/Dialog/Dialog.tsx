import Button, { type ButtonProps } from "@mui/material/Button";
import MuiDialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import type { ReactNode } from "react";
import { useDialog } from "./useDialog";

/** The props {@link Dialog} takes. */
export interface DialogProps {
  /** Whether the dialog is shown. */
  open: boolean;
  /** The dialog's title, which also names it to assistive technology. */
  title: ReactNode;
  /** The dialog's content, between its title and its actions. */
  children?: ReactNode;
  /** The label of the confirming action, such as **Save** or **Delete**. */
  confirmLabel: string;
  /** The confirming action's colour: `error` for one that destroys something. */
  confirmColor?: ButtonProps["color"];
  /**
   * Disables the confirming action alone, while what it confirms is not yet complete, such as a
   * name that has not been typed exactly.
   */
  confirmDisabled?: boolean;
  /** Called when the confirming action is chosen. */
  onConfirm: () => void;
  /** Called when **Cancel** is chosen, or the dialog is dismissed with **Escape** or its backdrop. */
  onCancel: () => void;
  /**
   * Whether the confirmed request is in flight. While it is, the dialog stays open: both actions
   * are disabled, and **Escape** and the backdrop do nothing.
   */
  pending?: boolean;
}

/**
 * A standard dialog: a title, content, and the actions **Cancel** (text) and a confirming action
 * (contained). It shows no errors itself: a dialog closes on failure, and the page or form behind
 * it shows the error.
 */
export function Dialog({
  open,
  title,
  children,
  confirmLabel,
  confirmColor = "primary",
  confirmDisabled = false,
  onConfirm,
  onCancel,
  pending = false,
}: DialogProps) {
  const dialog = useDialog({ confirmDisabled, onCancel, pending });
  return (
    <MuiDialog open={open} fullWidth maxWidth="sm" onClose={dialog.onClose}>
      <DialogTitle>{title}</DialogTitle>
      {children !== undefined && <DialogContent>{children}</DialogContent>}
      <DialogActions>
        <Button disabled={dialog.cancelDisabled} onClick={onCancel}>
          Cancel
        </Button>
        <Button
          variant="contained"
          color={confirmColor}
          disabled={dialog.confirmDisabled}
          onClick={onConfirm}
        >
          {confirmLabel}
        </Button>
      </DialogActions>
    </MuiDialog>
  );
}
