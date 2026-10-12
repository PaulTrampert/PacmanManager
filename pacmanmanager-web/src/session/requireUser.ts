import { redirect, type ParsedLocation } from "@tanstack/react-router";
import type { CurrentUser } from "../api";
import { loginUrl } from "./loginUrl";
import type { Session } from "./session";

/** What {@link requireUser} reads from the `beforeLoad` it guards. */
export interface RequireUserOptions {
  context: { session: Session };
  location: Pick<ParsedLocation, "href">;
}

/**
 * The `beforeLoad` guard for a route that needs a signed-in user. For an anonymous session it does a
 * full page navigation to `/login`, which returns the user to the requested path and query string
 * once they have signed in. For a signed-in session it puts the user on the route's context.
 *
 * @example
 * export const Route = createFileRoute("/repositories/new")({ beforeLoad: requireUser });
 */
export function requireUser({ context, location }: RequireUserOptions): {
  user: CurrentUser;
} {
  if (context.session.status === "anonymous") {
    // `/login` is the web host's, not the router's, so this leaves the app.
    throw redirect({ href: loginUrl(location.href), reloadDocument: true });
  }
  return { user: context.session.user };
}
