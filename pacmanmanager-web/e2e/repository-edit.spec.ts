import type { Page } from "@playwright/test";
import {
  antiforgeryHeaders,
  expect,
  signIn,
  test,
  uniqueName,
  users,
} from "./fixtures.ts";

/** Creates a repository named `name` through `page`'s session, and returns its id. */
async function createRepository(
  page: Page,
  name: string,
  { isPublic = false }: { isPublic?: boolean } = {},
): Promise<string> {
  const created = await page.request.post(
    "/api/pacmanmanager/v1/Repositories",
    { headers: await antiforgeryHeaders(page), data: { name, isPublic } },
  );
  expect(created.status(), await created.text()).toBe(201);
  return ((await created.json()) as { id: string }).id;
}

/** Opens the edit page of the repository `id`, and waits for its form. */
async function gotoEdit(page: Page, id: string, name: string): Promise<void> {
  await page.goto(`/repositories/${id}/edit`);
  await expect(page.getByRole("textbox", { name: "Name" })).toHaveValue(name);
}

/** Replaces the name in the edit form with `name`. */
async function rename(page: Page, name: string): Promise<void> {
  await page.getByRole("textbox", { name: "Name" }).fill(name);
}

/** Presses the form's **Save**, then the confirmation's **Save**. */
async function saveAndConfirm(page: Page): Promise<void> {
  await page.getByRole("main").getByRole("button", { name: "Save" }).click();
  const dialog = page.getByRole("dialog", { name: "Save these changes?" });
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "Save" }).click();
}

test("renaming a repository shows its new name on its page and in the list", async ({
  page,
}) => {
  await signIn(page, users.test);
  const name = uniqueName("e2e-edit-rename");
  const renamed = `${name}-renamed`;
  const id = await createRepository(page, name);

  await gotoEdit(page, id, name);
  await rename(page, renamed);
  await saveAndConfirm(page);

  await expect(page).toHaveURL(`/repositories/${id}`);
  await expect(
    page.getByRole("main").getByRole("heading", { level: 1, name: renamed }),
  ).toBeVisible();

  await page.goto(
    `/repositories?${new URLSearchParams({ nameContains: name })}`,
  );
  const table = page
    .getByRole("main")
    .getByRole("table", { name: "Repositories" });
  await expect(table.getByRole("link", { name: renamed })).toBeVisible();
  await expect(
    table.getByRole("link", { name, exact: true }),
  ).not.toBeVisible();
});

test("making a repository private hides it from other users", async ({
  page,
  browser,
}) => {
  await signIn(page, users.test);
  const name = uniqueName("e2e-edit-private");
  const id = await createRepository(page, name, { isPublic: true });

  const otherContext = await browser.newContext();
  try {
    const otherPage = await otherContext.newPage();
    await signIn(otherPage, users.other);
    await otherPage.goto(`/repositories/${id}`);
    await expect(
      otherPage.getByRole("main").getByRole("heading", { level: 1, name }),
    ).toBeVisible();

    await gotoEdit(page, id, name);
    await page.getByRole("radio", { name: "Private" }).check();
    await saveAndConfirm(page);
    await expect(page).toHaveURL(`/repositories/${id}`);

    await otherPage.reload();
    await expect(
      otherPage
        .getByRole("main")
        .getByRole("heading", { name: "Page not found" }),
    ).toBeVisible();
  } finally {
    await otherContext.close();
  }
});

test("renaming a repository to a taken name closes the confirmation and shows the error on the name field", async ({
  page,
}) => {
  await signIn(page, users.test);
  const name = uniqueName("e2e-edit-taken");
  const taken = `${name}-taken`;
  const id = await createRepository(page, name);
  await createRepository(page, taken);

  await gotoEdit(page, id, name);
  await rename(page, taken);
  await saveAndConfirm(page);

  await expect(
    page.getByRole("dialog", { name: "Save these changes?" }),
  ).not.toBeVisible();
  const field = page.getByRole("textbox", { name: "Name" });
  await expect(field).toHaveAttribute("aria-invalid", "true");
  await expect(field).toHaveAccessibleDescription(
    "A repository with this name already exists.",
  );
  await expect(field).toHaveValue(taken);
  await expect(page).toHaveURL(`/repositories/${id}/edit`);
});

test("cancelling an edit returns to the repository's page without saving", async ({
  page,
}) => {
  await signIn(page, users.test);
  const name = uniqueName("e2e-edit-cancel");
  const id = await createRepository(page, name);

  await gotoEdit(page, id, name);
  await rename(page, `${name}-unsaved`);
  await page.getByRole("main").getByRole("button", { name: "Cancel" }).click();

  await expect(page).toHaveURL(`/repositories/${id}`);
  await expect(
    page.getByRole("main").getByRole("heading", { level: 1, name }),
  ).toBeVisible();
  await page.reload();
  await expect(
    page.getByRole("main").getByRole("heading", { level: 1, name }),
  ).toBeVisible();
});
