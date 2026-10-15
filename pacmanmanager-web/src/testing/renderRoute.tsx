import { createMemoryHistory, RouterProvider } from "@tanstack/react-router";
import { cleanup, render } from "@testing-library/react";
import { beforeAll } from "vitest";
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

/**
 * Warms up every file that renders routes, before its first test. A route's component is split
 * from the route tree and loaded on its first render, and MUI sets up its styles on the first render
 * of all; together they made the first test of a file several times slower than the rest, slow
 * enough on CI to time out. This loads every route's chunks and renders the providers once, under
 * the hook timeout instead of a test's.
 */
beforeAll(async () => {
  const queryClient = createQueryClient();
  const router = createAppRouter({
    queryClient,
    history: createMemoryHistory(),
  });
  await Promise.all(
    Object.values(router.routesById).map((route) =>
      router.loadRouteChunk(route),
    ),
  );
  render(
    <AppProviders queryClient={queryClient}>
      <div />
    </AppProviders>,
  );
  cleanup();
}, 30_000);
