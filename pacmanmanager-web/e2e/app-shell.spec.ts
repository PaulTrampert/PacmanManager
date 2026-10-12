import { expect, test } from "./fixtures.ts";

test("an anonymous visitor loads / and sees the app shell", async ({
  page,
}) => {
  await page.goto("/");

  await expect(
    page.getByRole("banner").getByRole("link", { name: "PacmanManager" }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("main")
      .getByRole("heading", { name: "Welcome to PacmanManager" }),
  ).toBeVisible();
});
