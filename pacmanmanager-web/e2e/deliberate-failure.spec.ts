import { expect, test } from "./fixtures.ts";

// Deliberately failing, to prove the e2e job uploads its report and compose logs. Reverted next.
test("deliberately fails", async ({ page }) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "No such heading" }),
  ).toBeVisible({ timeout: 2_000 });
});
