import AppBar from "@mui/material/AppBar";
import Container from "@mui/material/Container";
import Link from "@mui/material/Link";
import Toolbar from "@mui/material/Toolbar";
import { Link as RouterLink } from "@tanstack/react-router";
import type { ReactNode } from "react";

/** The props {@link AppShell} takes. */
export interface AppShellProps {
  /** The page, rendered in the main landmark below the app bar. */
  children: ReactNode;
}

/** The frame around every page: the app bar, with the product name linking home, above the page. */
export function AppShell({ children }: AppShellProps) {
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
        </Toolbar>
      </AppBar>
      <Container component="main" sx={{ py: 3 }}>
        {children}
      </Container>
    </>
  );
}
