import type { Middleware } from "openapi-fetch";
import { loginUrl } from "../session/loginUrl";

/** Leaves the app for `url` by a full page navigation. */
export type Navigate = (url: string) => void;

/** Where the browser is: the current path and query string. */
function currentLocation(): string {
  return `${window.location.pathname}${window.location.search}`;
}

/**
 * Sends the browser to `/login?redirectUri=<current path and query>` when the API answers `401`.
 *
 * The web host answers a proxied call with `401` only when a signed-in user's session can no longer
 * be refreshed, and ends that session; a visitor who never signed in is proxied anonymously instead.
 * So a `401` here always means "sign in again", unlike `/userinfo`'s, which the session query fetches
 * outside this client.
 *
 * The response is never handed back: the call stays pending while the browser leaves, so nothing
 * renders an error for a request whose only outcome is signing in.
 *
 * @param navigate how to leave the app; a full page navigation unless a test replaces it.
 */
export function signInOnUnauthorizedMiddleware(
  navigate: Navigate = (url) => window.location.assign(url),
): Middleware {
  return {
    onResponse({ response }) {
      if (response.status !== 401) {
        return undefined;
      }
      navigate(loginUrl(currentLocation()));
      return new Promise<never>(() => {});
    },
  };
}
