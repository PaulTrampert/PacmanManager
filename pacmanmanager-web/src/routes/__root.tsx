import { useQueryErrorResetBoundary } from "@tanstack/react-query";
import {
  createRootRouteWithContext,
  Outlet,
  useRouter,
  type ErrorComponentProps,
} from "@tanstack/react-router";
import { AppShell } from "../components/AppShell";
import { ErrorPage } from "../components/ErrorPage";
import { NotFoundPage } from "../components/NotFoundPage";
import type { RouterContext } from "../router";
import { sessionQuery, useSession } from "../session";

// The one error component and the one not-found component, which a route's failure bubbles up to
// unless the route has a component of its own for it. The not-found component renders in the root
// component's outlet; the error component replaces the root component, so it draws the app shell
// itself.
export const Route = createRootRouteWithContext<RouterContext>()({
  // No route renders until the session has resolved, so a page never shows the anonymous view and
  // then flips to the signed-in one.
  beforeLoad: async ({ context }) => ({
    session: await context.queryClient.ensureQueryData(sessionQuery()),
  }),
  component: RootComponent,
  errorComponent: RootErrorComponent,
  notFoundComponent: NotFoundPage,
});

function RootComponent() {
  const session = useSession();
  return (
    <AppShell session={session}>
      <Outlet />
    </AppShell>
  );
}

function RootErrorComponent({ error }: ErrorComponentProps) {
  const router = useRouter();
  const queryErrorResetBoundary = useQueryErrorResetBoundary();
  const retry = () => {
    // Let a suspense query that failed fetch again, then rerun the loaders, which also resets this
    // error boundary.
    queryErrorResetBoundary.reset();
    void router.invalidate();
  };
  return (
    <AppShell>
      <ErrorPage error={error} onRetry={retry} />
    </AppShell>
  );
}
