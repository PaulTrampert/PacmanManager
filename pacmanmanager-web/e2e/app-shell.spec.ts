import { expect, test } from "./fixtures.ts";

test("an anonymous visitor loads / and sees the app shell around the repository list", async ({
  page,
}) => {
  await page.goto("/");

  await expect(page).toHaveURL(/\/repositories$/);
  await expect(
    page.getByRole("banner").getByRole("link", { name: "PacmanManager" }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("main")
      .getByRole("heading", { level: 1, name: "Repositories" }),
  ).toBeVisible();
});
