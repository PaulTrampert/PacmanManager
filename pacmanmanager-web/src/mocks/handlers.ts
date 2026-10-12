import type { RequestHandler } from "msw";

/**
 * The request handlers every unit test and story starts with. A test or story adds its own with
 * `server.use(...)` or `msw.use(...)`; anything left unmatched is an error.
 */
export const handlers: RequestHandler[] = [];
