import { useState, type ChangeEvent } from "react";
import type { Repository } from "../../api";

/** What {@link useDeleteRepositoryDialog} gives the dialog to render. */
export interface DeleteRepositoryDialogState {
  /** What has been typed into the confirmation field since the dialog last opened. */
  typed: string;
  /** Handles a change to the confirmation field. */
  onTypedChange: (event: ChangeEvent<HTMLInputElement>) => void;
  /**
   * Whether what has been typed is the repository's name, exactly and case included. Nothing typed
   * never confirms, even for a repository whose name is missing.
   */
  confirmed: boolean;
}

/**
 * The behaviour of `DeleteRepositoryDialog`: the typed confirmation, which must equal the
 * repository's name exactly before **Delete** is enabled, and which starts empty each time the
 * dialog opens, so retrying after a failure means typing the name again.
 *
 * @param name the repository's name, which the typed text must match. The schema marks it
 *   optional, though the API always sends it; without it, nothing confirms.
 * @param open whether the dialog is shown.
 */
export function useDeleteRepositoryDialog(
  name: Repository["name"],
  open: boolean,
): DeleteRepositoryDialogState {
  const [typed, setTyped] = useState("");
  // The open state last rendered, so that opening again clears what was typed. Adjusting state
  // while rendering, rather than in an effect, means the cleared field is the first thing shown.
  const [wasOpen, setWasOpen] = useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) {
      setTyped("");
    }
  }

  return {
    typed,
    onTypedChange: (event) => {
      setTyped(event.target.value);
    },
    confirmed: typed !== "" && typed === name,
  };
}
