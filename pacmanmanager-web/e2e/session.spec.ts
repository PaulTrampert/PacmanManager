import { expect, submitSignInForm, test, users } from "./fixtures.ts";
import { webHostUrl } from "./stack.ts";

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
