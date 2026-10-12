import { createMemoryHistory, RouterProvider } from "@tanstack/react-router";
import { render } from "@testing-library/react";
import { AppProviders } from "../AppProviders";
import { createQueryClient } from "../queryClient";
import { createAppRouter } from "../router";

/**
 * Renders the whole app, with its real route tree and providers, at `path`. The returned router
 * and query client let a test navigate or inspect the cache.
 */
export function renderRoute(path: string) {
  const queryClient = createQueryClient();
  const router = createAppRouter({
    queryClient,
    history: createMemoryHistory({ initialEntries: [path] }),
  });
  const result = render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return { ...result, router, queryClient };
}
