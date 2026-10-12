import type { RequestHandler } from "msw";
import { antiforgeryHandler } from "./api";
import { anonymousUserinfoHandler } from "./session";

/**
 * The request handlers every unit test and story starts with. A test or story adds its own with
 * `server.use(...)` or `msw.use(...)`; anything left unmatched is an error.
 *
 * `GET /antiforgery` is answered here, since every mutation the client sends fetches it first, and
 * `GET /userinfo` as for an anonymous visitor, since the root route resolves the session on every
 * load. A test of a signed-in user adds `signedInHandlers(user)`.
 */
export const handlers: RequestHandler[] = [
  antiforgeryHandler(),
  anonymousUserinfoHandler(),
];
