// The web host's session endpoints. Kept apart from the modules that use them, which import the API
// client, so the mock handlers can name them without creating the client (see testing/setup.ts).

/** The web host's endpoint that answers with the signed-in user's claims, or `401`. */
export const userinfoPath = "/userinfo";

/** The web host's sign-in endpoint. It is a web host route, not a router route. */
export const loginPath = "/login";

/** The web host's sign-out endpoint, which takes a form `POST`. */
export const logoutPath = "/logout";

/** The form field the web host reads the antiforgery token from on sign-out. */
export const antiforgeryField = "antiforgeryToken";
