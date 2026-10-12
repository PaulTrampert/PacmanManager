import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useLocation } from "@tanstack/react-router";
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
 * they are on, or the signed-in user's display name with **Sign out**.
 */
export function SessionControl({
  session,
  onSignOut = signOut,
}: SessionControlProps) {
  const { pathname, searchStr } = useLocation();

  if (session.status === "anonymous") {
    return (
      <Button color="inherit" href={loginUrl(`${pathname}${searchStr}`)}>
        Sign in
      </Button>
    );
  }

  return (
    <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
      <Typography>{session.user.displayName}</Typography>
      <Button color="inherit" onClick={() => void onSignOut()}>
        Sign out
      </Button>
    </Stack>
  );
}
