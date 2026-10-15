// Shared helpers for the end-to-end tests. Tests import `test` and `expect` from here rather than
// from @playwright/test, so fixtures added later reach every test.
import { expect, test, type Page } from "@playwright/test";
import {
  antiforgeryCookie,
  antiforgeryHeader,
  antiforgeryPath,
} from "../src/api/antiforgery.ts";
import { webHostUrl } from "./stack.ts";

export { expect, test };

/** A user in the `localdev` realm (keycloak/localdev.json). */
export interface RealmUser {
  /** The username typed into Keycloak's sign-in form. */
  username: string;
  /** The user's password. */
  password: string;
}

/** The realm's test users. Tests sign in as these and never create users of their own. */
export const users = {
  test: { username: "test", password: "Asdfasdf1" },
  other: { username: "other", password: "Asdfasdf1" },
} as const satisfies Record<string, RealmUser>;

/**
 * Signs `user` in through the web host's `/login` and the real Keycloak sign-in form, and waits until
 * the browser is back on the web host at `path` (the repository list by default, where `/`
 * redirects).
 */
export async function signIn(
  page: Page,
  user: RealmUser,
  path = "/repositories",
): Promise<void> {
  await page.goto(`/login?${new URLSearchParams({ redirectUri: path })}`);
  await submitSignInForm(page, user, path);
}

/**
 * Fills in and submits the Keycloak sign-in form the page is showing as `user`, and waits until the
 * browser is back on the web host at `path`, a path with its query string.
 */
export async function submitSignInForm(
  page: Page,
  user: RealmUser,
  path: string,
): Promise<void> {
  await page.getByLabel("Username or email").fill(user.username);
  await page.getByLabel("Password", { exact: true }).fill(user.password);
  await page.getByRole("button", { name: "Sign In" }).click();
  await page.waitForURL(
    (url) =>
      url.origin === webHostUrl && `${url.pathname}${url.search}` === path,
  );
}

/**
 * A name no other test, and no earlier run against the same database, has used: `prefix` followed
 * by the time and a random suffix. Tests share one stack, so whatever they create is named with this.
 */
export function uniqueName(prefix: string): string {
  const random = Math.random().toString(36).slice(2, 8);
  return `${prefix}-${Date.now().toString(36)}-${random}`;
}

/**
 * The headers a mutation sent through `page.request` needs to pass the web host's antiforgery check:
 * fetches `/antiforgery` in the page's cookie session, and returns its `XSRF-TOKEN` cookie as the
 * `X-XSRF-TOKEN` header.
 */
export async function antiforgeryHeaders(
  page: Page,
): Promise<Record<string, string>> {
  const response = await page.request.get(antiforgeryPath);
  expect(response.ok(), `GET ${antiforgeryPath}`).toBe(true);
  const cookie = (await page.context().cookies(webHostUrl)).find(
    ({ name }) => name === antiforgeryCookie,
  );
  expect(cookie, `the ${antiforgeryCookie} cookie`).toBeDefined();
  return { [antiforgeryHeader]: decodeURIComponent(cookie!.value) };
}
