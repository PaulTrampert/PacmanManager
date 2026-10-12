import AppBar from "@mui/material/AppBar";
import Box from "@mui/material/Box";
import Container from "@mui/material/Container";
import Link from "@mui/material/Link";
import Toolbar from "@mui/material/Toolbar";
import { Link as RouterLink } from "@tanstack/react-router";
import type { ReactNode } from "react";
import type { Session } from "../../session";
import { SessionControl } from "../SessionControl";

/** The props {@link AppShell} takes. */
export interface AppShellProps {
  /** The session the app bar's session control shows. */
  session: Session;
  /** The page, rendered in the main landmark below the app bar. */
  children: ReactNode;
}

/**
 * The frame around every page: the app bar, with the product name linking home and the session
 * control, above the page.
 */
export function AppShell({ session, children }: AppShellProps) {
  return (
    <>
      <AppBar position="static">
        <Toolbar>
          <Link
            component={RouterLink}
            to="/"
            variant="h6"
            color="inherit"
            underline="none"
          >
            PacmanManager
          </Link>
          <Box sx={{ flexGrow: 1 }} />
          <SessionControl session={session} />
        </Toolbar>
      </AppBar>
      <Container component="main" sx={{ py: 3 }}>
        {children}
      </Container>
    </>
  );
}
