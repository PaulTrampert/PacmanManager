import { createRootRouteWithContext, Outlet } from "@tanstack/react-router";
import { AppShell } from "../components/AppShell";
import type { RouterContext } from "../router";
import { sessionQuery, useSession } from "../session";

export const Route = createRootRouteWithContext<RouterContext>()({
  // No route renders until the session has resolved, so a page never shows the anonymous view and
  // then flips to the signed-in one.
  beforeLoad: async ({ context }) => ({
    session: await context.queryClient.ensureQueryData(sessionQuery()),
  }),
  component: RootComponent,
});

function RootComponent() {
  const session = useSession();
  return (
    <AppShell session={session}>
      <Outlet />
    </AppShell>
  );
}
