import Button from "@mui/material/Button";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import { useLocation } from "@tanstack/react-router";
import { useState } from "react";
import { loginUrl, signOut, type Session } from "../../session";

/** The props {@link SessionControl} takes. */
export interface SessionControlProps {
  /** The session to show. */
  session: Session;
  /** Signs the user out; the real sign-out unless a story or test replaces it. */
  onSignOut?: () => void | Promise<void>;
}

/**
 * The app bar's session control: **Sign in** for an anonymous visitor, returning them to the page
 * they are on, or the signed-in user's display name, which opens a menu whose only item for now
 * is **Sign out**.
 */
export function SessionControl({
  session,
  onSignOut = signOut,
}: SessionControlProps) {
  const { pathname, searchStr } = useLocation();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  if (session.status === "anonymous") {
    return (
      <Button color="inherit" href={loginUrl(`${pathname}${searchStr}`)}>
        Sign in
      </Button>
    );
  }

  const close = () => setAnchor(null);

  return (
    <>
      <Button
        color="inherit"
        id="session-menu-button"
        aria-haspopup="menu"
        aria-controls={anchor ? "session-menu" : undefined}
        aria-expanded={anchor ? "true" : undefined}
        onClick={(event) => setAnchor(event.currentTarget)}
      >
        {session.user.displayName}
      </Button>
      <Menu
        id="session-menu"
        anchorEl={anchor}
        open={anchor !== null}
        onClose={close}
        slotProps={{ list: { "aria-labelledby": "session-menu-button" } }}
      >
        <MenuItem
          onClick={() => {
            close();
            void onSignOut();
          }}
        >
          Sign out
        </MenuItem>
      </Menu>
    </>
  );
}
