import CssBaseline from "@mui/material/CssBaseline";
import { ThemeProvider } from "@mui/material/styles";
import { QueryClientProvider, type QueryClient } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { theme } from "./theme";

/** The props {@link AppProviders} takes. */
export interface AppProvidersProps {
  queryClient: QueryClient;
  children: ReactNode;
}

/**
 * The providers every rendering of the app needs, around the router: the MUI theme and the query
 * client. Shared by the app, the unit tests and Storybook so that all three render the same way.
 */
export function AppProviders({ queryClient, children }: AppProvidersProps) {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </ThemeProvider>
  );
}
