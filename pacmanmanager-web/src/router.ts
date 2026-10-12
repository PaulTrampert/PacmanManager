import type { QueryClient } from "@tanstack/react-query";
import { createRouter, type RouterHistory } from "@tanstack/react-router";
import { routeTree } from "./routeTree.gen";

/** What every route can read from the router context. */
export interface RouterContext {
  queryClient: QueryClient;
}

/** The options {@link createAppRouter} takes. */
export interface AppRouterOptions {
  queryClient: QueryClient;
  /** The history to drive the router with; the browser's when omitted. */
  history?: RouterHistory;
}

/**
 * Creates the app's router over the generated route tree. Preloading on intent is always stale as
 * far as the router is concerned, so TanStack Query, not the router, decides freshness.
 */
export function createAppRouter({ queryClient, history }: AppRouterOptions) {
  return createRouter({
    routeTree,
    context: { queryClient },
    history,
    defaultPreload: "intent",
    defaultPreloadStaleTime: 0,
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}
