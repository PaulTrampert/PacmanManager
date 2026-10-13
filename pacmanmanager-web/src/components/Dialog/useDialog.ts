/** What {@link useDialog} needs from the dialog's props. */
export interface UseDialogOptions {
  /** Disables the confirming action alone. */
  confirmDisabled: boolean;
  /** Called when the dialog is cancelled or dismissed. */
  onCancel: () => void;
  /** Whether the confirmed request is in flight. */
  pending: boolean;
}

/** What {@link useDialog} gives the dialog to render. */
export interface DialogState {
  /** Handles **Escape** and the backdrop: cancels, unless a request is in flight. */
  onClose: () => void;
  /** Whether **Cancel** is disabled. */
  cancelDisabled: boolean;
  /** Whether the confirming action is disabled. */
  confirmDisabled: boolean;
}

/**
 * The behaviour of `Dialog`: while its request is in flight, both actions are disabled and
 * dismissing it does nothing.
 */
export function useDialog({
  confirmDisabled,
  onCancel,
  pending,
}: UseDialogOptions): DialogState {
  return {
    onClose: () => {
      if (!pending) {
        onCancel();
      }
    },
    cancelDisabled: pending,
    confirmDisabled: pending || confirmDisabled,
  };
}
