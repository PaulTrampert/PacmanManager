/**
 * The path prefixes the web host (SpaHost) answers itself rather than serving the app: the proxied
 * API and the sign-in, sign-out, user-info and antiforgery endpoints. The Vite dev server proxies
 * them to the compose stack, and Storybook's mock service worker treats an unmocked request to one
 * of them as an error.
 */
export const webHostPaths = [
  "/api",
  "/login",
  "/logout",
  "/userinfo",
  "/antiforgery",
  "/signin-oidc",
  "/signout-callback-oidc",
] as const;

/** Whether `pathname` is one the web host answers itself; see {@link webHostPaths}. */
export function isWebHostPath(pathname: string): boolean {
  return webHostPaths.some(
    (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
  );
}
