import type { Middleware } from "openapi-fetch";

/** The web host's endpoint that issues an antiforgery token, as the {@link antiforgeryCookie}. */
export const antiforgeryPath = "/antiforgery";

/** The cookie `GET /antiforgery` sets the token in. */
export const antiforgeryCookie = "XSRF-TOKEN";

/** The request header the web host reads the token from. */
export const antiforgeryHeader = "X-XSRF-TOKEN";

/** The methods that change nothing, and so carry no antiforgery token. */
const safeMethods = new Set(["GET", "HEAD", "OPTIONS"]);

/** Thrown when the web host does not issue an antiforgery token for a mutation to send. */
export class AntiforgeryError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "AntiforgeryError";
  }
}

/** The value of the cookie called `name` in `cookies` (a `document.cookie` string), if set. */
export function readCookie(cookies: string, name: string): string | undefined {
  for (const cookie of cookies.split(";")) {
    const separator = cookie.indexOf("=");
    if (separator !== -1 && cookie.slice(0, separator).trim() === name) {
      return decodeURIComponent(cookie.slice(separator + 1).trim());
    }
  }
  return undefined;
}

/**
 * Fetches a fresh antiforgery token from the web host and returns it.
 *
 * @throws {AntiforgeryError} when the web host answers with an error or sets no token.
 */
export async function fetchAntiforgeryToken(): Promise<string> {
  // Looked up on each call rather than captured, so a mock installed after import still applies.
  const response = await globalThis.fetch(antiforgeryPath, {
    credentials: "same-origin",
  });
  if (!response.ok) {
    throw new AntiforgeryError(
      `GET ${antiforgeryPath} failed with status ${response.status}.`,
    );
  }
  const token = readCookie(document.cookie, antiforgeryCookie);
  if (!token) {
    throw new AntiforgeryError(
      `GET ${antiforgeryPath} did not set the ${antiforgeryCookie} cookie.`,
    );
  }
  return token;
}

/**
 * Sends an antiforgery token with every request but a `GET`, `HEAD` or `OPTIONS`: it fetches
 * `/antiforgery` first, then copies the `XSRF-TOKEN` cookie's value into the `X-XSRF-TOKEN` header.
 * The token is fetched afresh for each mutation and never cached, so it always belongs to the
 * current cookie session.
 */
export const antiforgeryMiddleware: Middleware = {
  async onRequest({ request }) {
    if (safeMethods.has(request.method.toUpperCase())) {
      return undefined;
    }
    request.headers.set(antiforgeryHeader, await fetchAntiforgeryToken());
    return request;
  },
};
