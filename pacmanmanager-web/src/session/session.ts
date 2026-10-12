import { queryOptions } from "@tanstack/react-query";
import { client, unwrap, type CurrentUser } from "../api";
import { userinfoPath } from "./paths";

/** A visitor who has not signed in to the web host. */
export interface AnonymousSession {
  status: "anonymous";
}

/** A user signed in to the web host, with their own user record from the API. */
export interface SignedInSession {
  status: "signedIn";
  user: CurrentUser;
}

/** Who the app is being used by: an anonymous visitor or a signed-in user. */
export type Session = AnonymousSession | SignedInSession;

/** Thrown when the web host answers `/userinfo` with neither `200` nor `401`. */
export class SessionError extends Error {
  /** The status the web host answered with. */
  readonly status: number;

  constructor(status: number) {
    super(`GET ${userinfoPath} failed with status ${status}.`);
    this.name = "SessionError";
    this.status = status;
  }
}

/**
 * Resolves the session. `GET /userinfo` decides whether the visitor is signed in at all: a `401`
 * there means anonymous, and is not a reason to sign in, so it is fetched with plain `fetch`, outside
 * the API client. A signed-in user's record is then fetched from the API, which is also what
 * provisions it on a new user's first sign-in.
 *
 * @throws {SessionError} when `/userinfo` answers with anything but `200` or `401`.
 * A `401` from the API means the user's session can no longer be refreshed; the API client then sends
 * the browser to sign in, and this never settles.
 *
 * @throws {ApiError} when the API does not answer `/v1/Users/me` with the user's record.
 */
export async function fetchSession(): Promise<Session> {
  // Looked up on each call rather than captured, so a mock installed after import still applies.
  const response = await globalThis.fetch(userinfoPath, {
    credentials: "same-origin",
  });
  if (response.status === 401) {
    return { status: "anonymous" };
  }
  if (!response.ok) {
    throw new SessionError(response.status);
  }
  const user = await unwrap(client.GET("/v1/Users/me"));
  return { status: "signedIn", user };
}

/**
 * The session's query options. The root route resolves it in `beforeLoad`, before any route renders,
 * and {@link useSession} reads it from the cache.
 */
export function sessionQuery() {
  return queryOptions({
    queryKey: ["session"],
    queryFn: fetchSession,
  });
}
