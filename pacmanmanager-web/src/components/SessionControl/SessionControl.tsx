import Button from "@mui/material/Button";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import { signOut, type Session } from "../../session";
import { useSessionControl } from "./useSessionControl";

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
  const control = useSessionControl(onSignOut);

  if (session.status === "anonymous") {
    return (
      <Button color="inherit" href={control.signInHref}>
        Sign in
      </Button>
    );
  }

  return (
    <>
      <Button
        color="inherit"
        id="session-menu-button"
        aria-haspopup="menu"
        aria-controls={control.isOpen ? "session-menu" : undefined}
        aria-expanded={control.isOpen ? "true" : undefined}
        onClick={control.open}
      >
        {session.user.displayName}
      </Button>
      <Menu
        id="session-menu"
        anchorEl={control.anchor}
        open={control.isOpen}
        onClose={control.close}
        slotProps={{ list: { "aria-labelledby": "session-menu-button" } }}
      >
        <MenuItem onClick={control.signOut}>Sign out</MenuItem>
      </Menu>
    </>
  );
}
