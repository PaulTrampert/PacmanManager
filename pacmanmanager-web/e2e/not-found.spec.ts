import { expect, test } from "./fixtures.ts";

test("an unknown path shows the not-found page", async ({ page }) => {
  await page.goto(`/no-such-page-${crypto.randomUUID()}`);

  await expect(
    page.getByRole("main").getByRole("heading", { name: "Page not found" }),
  ).toBeVisible();
  await expect(
    page.getByRole("banner").getByRole("link", { name: "PacmanManager" }),
  ).toBeVisible();

  await page.getByRole("link", { name: "Go to the home page" }).click();

  await expect(page).toHaveURL(/\/repositories$/);
  await expect(
    page
      .getByRole("main")
      .getByRole("heading", { level: 1, name: "Repositories" }),
  ).toBeVisible();
});
