import type { RequestHandler } from "msw";
import { antiforgeryHandler } from "./api";

/**
 * The request handlers every unit test and story starts with. A test or story adds its own with
 * `server.use(...)` or `msw.use(...)`; anything left unmatched is an error.
 *
 * `GET /antiforgery` is answered here, since every mutation the client sends fetches it first.
 */
export const handlers: RequestHandler[] = [antiforgeryHandler()];
