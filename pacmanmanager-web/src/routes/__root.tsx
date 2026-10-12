import { createRootRouteWithContext, Outlet } from "@tanstack/react-router";
import { AppShell } from "../components/AppShell";
import type { RouterContext } from "../router";

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootComponent,
});

function RootComponent() {
  return (
    <AppShell>
      <Outlet />
    </AppShell>
  );
}
