import type { Page } from "@playwright/test";
import { expect, signIn, submitSignInForm, test, users } from "./fixtures.ts";
import {
  setAccessTokenLifespan,
  signOutSessions,
  webHostClientId,
} from "./keycloak.ts";
import { keycloakUrl, webHostUrl } from "./stack.ts";

test("an anonymous visitor sees Sign in and no display name", async ({
  page,
}) => {
  await page.goto("/");

  const banner = page.getByRole("banner");
  await expect(banner.getByRole("link", { name: "Sign in" })).toBeVisible();
  await expect(
    banner.getByRole("button", { name: "Sign out" }),
  ).not.toBeVisible();
});

test("a user signs in from a deep link, lands back on it, and signs out again", async ({
  page,
}) => {
  const deepLink = "/repositories?offset=0&pageSize=10";
  await page.goto(deepLink);

  await page.getByRole("banner").getByRole("link", { name: "Sign in" }).click();
  await submitSignInForm(page, users.test, deepLink);

  // The display name is the user's own record, which another test may have changed.
  const me = await page.request.get("/api/pacmanmanager/v1/Users/me");
  expect(me.ok()).toBe(true);
  const { displayName } = (await me.json()) as { displayName: string };
  const banner = page.getByRole("banner");
  const menuButton = banner.getByRole("button", { name: displayName });
  await expect(menuButton).toBeVisible();
  await expect(banner.getByRole("link", { name: "Sign in" })).not.toBeVisible();

  await menuButton.click();
  await page.getByRole("menuitem", { name: "Sign out" }).click();

  await page.waitForURL(
    (url) => url.origin === webHostUrl && url.pathname === "/",
  );
  await expect(banner.getByRole("link", { name: "Sign in" })).toBeVisible();
  await expect(
    banner.getByText(displayName, { exact: true }),
  ).not.toBeVisible();

  await page.reload();

  await expect(banner.getByRole("link", { name: "Sign in" })).toBeVisible();
  await expect(
    banner.getByText(displayName, { exact: true }),
  ).not.toBeVisible();
});

/** Waits until the page's app bar shows the signed-in user's display name. */
async function expectSignedIn(page: Page): Promise<void> {
  // The display name is the user's own record, which another test may have changed.
  const me = await page.request.get("/api/pacmanmanager/v1/Users/me");
  expect(me.ok()).toBe(true);
  const { displayName } = (await me.json()) as { displayName: string };
  await expect(
    page.getByRole("banner").getByRole("button", { name: displayName }),
  ).toBeVisible();
}

// Signs the `other` user's sessions out in Keycloak, so no other test signs in as `other`.
test("a user whose session can no longer be refreshed is sent to sign in, and back", async ({
  page,
}) => {
  const deepLink = "/repositories?offset=0&pageSize=10";
  // The web host refreshes a token only once it is about to expire, and the realm's live for five
  // minutes. Tokens this short are refreshed on every request, so the dead session shows at once.
  // Another test signing in meanwhile gets them too, and is merely refreshed more often.
  const previousLifespan = await setAccessTokenLifespan(webHostClientId, 10);
  try {
    await signIn(page, users.other, deepLink);
    await expectSignedIn(page);

    await signOutSessions(users.other.username);
    await page.reload();

    // Sent to Keycloak's sign-in form, not shown the anonymous view.
    await page.waitForURL((url) => url.origin === keycloakUrl);
    await expect(page.getByLabel("Username or email")).toBeVisible();

    await submitSignInForm(page, users.other, deepLink);
    await expectSignedIn(page);
  } finally {
    await setAccessTokenLifespan(webHostClientId, previousLifespan);
  }
});
