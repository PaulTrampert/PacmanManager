import { expect, signIn, test, uniqueName, users } from "./fixtures.ts";

for (const visibility of ["Public", "Private"] as const) {
  test(`creating a ${visibility.toLowerCase()} repository lands on its page`, async ({
    page,
  }) => {
    await signIn(page, users.test, "/repositories/new");
    const name = uniqueName(`e2e-create-${visibility.toLowerCase()}`);
    const main = page.getByRole("main");

    await main.getByRole("textbox", { name: "Name" }).fill(name);
    await main.getByRole("radio", { name: visibility }).check();
    await main.getByRole("button", { name: "Create" }).click();

    await expect(page).toHaveURL(/\/repositories\/[0-9a-f-]{36}$/);
    await expect(main.getByRole("heading", { level: 1, name })).toBeVisible();
    await expect(main.getByLabel(`Visibility: ${visibility}`)).toBeVisible();
  });
}

test("creating a repository with a taken name shows the error on the name field", async ({
  page,
}) => {
  await signIn(page, users.test, "/repositories/new");
  const name = uniqueName("e2e-create-taken");
  const main = page.getByRole("main");
  const nameField = main.getByRole("textbox", { name: "Name" });

  await nameField.fill(name);
  await main.getByRole("button", { name: "Create" }).click();
  await expect(page).toHaveURL(/\/repositories\/[0-9a-f-]{36}$/);

  await page.goto("/repositories/new");
  await nameField.fill(name);
  await main.getByRole("button", { name: "Create" }).click();

  await expect(nameField).toHaveAccessibleDescription(
    "A repository with this name already exists.",
  );
  await expect(nameField).toHaveAttribute("aria-invalid", "true");
  await expect(main.getByRole("alert")).toHaveCount(0);
  await expect(page).toHaveURL("/repositories/new");
});

test("Cancel on the new repository page returns to the list", async ({
  page,
}) => {
  await signIn(page, users.test, "/repositories/new");

  await page.getByRole("main").getByRole("button", { name: "Cancel" }).click();

  await expect(page).toHaveURL("/repositories");
});
