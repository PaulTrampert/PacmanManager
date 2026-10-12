import { useQuery, useQueryErrorResetBoundary } from "@tanstack/react-query";
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
import { sessionQuery, useSession, type Session } from "../session";

const anonymousSession: Session = { status: "anonymous" };

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
  // The session is cached unless resolving it is what failed, and then the visitor is shown as
  // anonymous rather than failing again here.
  const { data: session = anonymousSession } = useQuery({
    ...sessionQuery(),
    enabled: false,
  });
  const retry = () => {
    // Let a suspense query that failed fetch again, then rerun the loaders, which also resets this
    // error boundary.
    queryErrorResetBoundary.reset();
    void router.invalidate();
  };
  return (
    <AppShell session={session}>
      <ErrorPage error={error} onRetry={retry} />
    </AppShell>
  );
}
