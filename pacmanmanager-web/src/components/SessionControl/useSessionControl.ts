import { useLocation } from "@tanstack/react-router";
import { useState } from "react";
import { loginUrl } from "../../session";

/** What {@link useSessionControl} returns to `SessionControl`. */
export interface SessionControlBehaviour {
  /** Where **Sign in** goes: the sign-in path, returning to the current path and query. */
  signInHref: string;
  /** The element the menu is anchored to, or `null` while it is closed. */
  anchor: HTMLElement | null;
  /** Whether the menu is open. */
  isOpen: boolean;
  /** Opens the menu, anchored to the element that was clicked. */
  open: (event: { currentTarget: HTMLElement }) => void;
  /** Closes the menu. */
  close: () => void;
  /** Closes the menu, then signs the user out with `onSignOut`. */
  signOut: () => void;
}

/**
 * The behaviour of `SessionControl`: the sign-in link for the page the visitor is on, and the
 * signed-in user's menu, which opens on the display name and closes before signing out.
 */
export function useSessionControl(
  onSignOut: () => void | Promise<void>,
): SessionControlBehaviour {
  const { pathname, searchStr } = useLocation();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  const close = () => setAnchor(null);

  return {
    signInHref: loginUrl(`${pathname}${searchStr}`),
    anchor,
    isOpen: anchor !== null,
    open: (event) => setAnchor(event.currentTarget),
    close,
    signOut: () => {
      close();
      void onSignOut();
    },
  };
}
