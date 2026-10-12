import { loginPath } from "./paths";

/**
 * The address that signs the user in and then returns them to `redirectUri`, a path on this site
 * with its query string.
 *
 * @example
 * loginUrl("/repositories?offset=20"); // "/login?redirectUri=%2Frepositories%3Foffset%3D20"
 */
export function loginUrl(redirectUri: string): string {
  return `${loginPath}?${new URLSearchParams({ redirectUri })}`;
}
