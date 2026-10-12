import createClient from "openapi-fetch";
import { antiforgeryMiddleware } from "./antiforgery";
import { apiBaseUrl } from "./apiPaths";
import type { paths } from "./schema.gen";

/**
 * The one API client. It calls RepoHost through the web host's proxy, with the browser's cookie
 * session; the web host attaches the access token. Nothing else calls `fetch` against `/api`.
 *
 * Call it through {@link unwrap}, which returns the data or throws an `ApiError`.
 */
export const client = createClient<paths>({
  baseUrl: apiBaseUrl,
  credentials: "same-origin",
  // Looked up on each call rather than captured when the client is created, so a mock installed
  // after this module loads (as MSW's is in the unit tests) still applies.
  fetch: (request) => globalThis.fetch(request),
});

client.use(antiforgeryMiddleware);
