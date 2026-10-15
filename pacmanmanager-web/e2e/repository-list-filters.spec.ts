import type { Locator, Page } from "@playwright/test";
import {
  antiforgeryHeaders,
  expect,
  signIn,
  test,
  uniqueName,
  users,
} from "./fixtures.ts";

/** Creates a repository named `name` through `page`'s session. */
async function createRepository(
  page: Page,
  name: string,
  { isPublic = false }: { isPublic?: boolean } = {},
): Promise<void> {
  const created = await page.request.post(
    "/api/pacmanmanager/v1/Repositories",
    { headers: await antiforgeryHeaders(page), data: { name, isPublic } },
  );
  expect(created.status(), await created.text()).toBe(201);
}

/** The repository table on the page. */
function repositoryTable(page: Page): Locator {
  return page.getByRole("main").getByRole("table", { name: "Repositories" });
}

/** The name cell of each of the table's rows, in order. */
function rowNames(page: Page): Locator {
  return repositoryTable(page).locator("tbody tr td:first-child");
}

/** The page's search params. */
function searchParams(page: Page): URLSearchParams {
  return new URL(page.url()).searchParams;
}

/** Waits until the page's URL has exactly `expected` as its search params. */
async function expectSearch(
  page: Page,
  expected: Record<string, string>,
): Promise<void> {
  await expect
    .poll(() => Object.fromEntries(searchParams(page)))
    .toEqual(expected);
}

/** Lists the repositories whose names contain `prefix`, the only ones a test may rely on. */
async function gotoList(page: Page, prefix: string, extra = {}): Promise<void> {
  await page.goto(
    `/repositories?${new URLSearchParams({ nameContains: prefix, ...extra })}`,
  );
}

test("filters the repository list by name, and finds the filter again on reload", async ({
  page,
}) => {
  await signIn(page, users.test);
  const prefix = uniqueName("filter-name");
  await createRepository(page, `${prefix}-alpha`);
  await createRepository(page, `${prefix}-beta`);

  await page.goto("/repositories");
  const name = page.getByRole("main").getByRole("textbox", { name: "Name" });
  await name.fill(`${prefix}-alpha`);

  await expectSearch(page, { nameContains: `${prefix}-alpha` });
  await expect(rowNames(page)).toHaveText([`${prefix}-alpha`]);

  await page.reload();
  await expect(name).toHaveValue(`${prefix}-alpha`);
  await expect(rowNames(page)).toHaveText([`${prefix}-alpha`]);
});

test("filters the repository list by visibility, and finds the filter again on reload", async ({
  page,
}) => {
  await signIn(page, users.test);
  const prefix = uniqueName("filter-visibility");
  await createRepository(page, `${prefix}-private`, { isPublic: false });
  await createRepository(page, `${prefix}-public`, { isPublic: true });

  await gotoList(page, prefix);
  await expect(rowNames(page)).toHaveText([
    `${prefix}-private`,
    `${prefix}-public`,
  ]);

  const visibility = page
    .getByRole("main")
    .getByRole("combobox", { name: "Visibility" });
  await visibility.click();
  await page.getByRole("option", { name: "Private" }).click();
  await expectSearch(page, { nameContains: prefix, isPublic: "false" });
  await expect(rowNames(page)).toHaveText([`${prefix}-private`]);

  await visibility.click();
  await page.getByRole("option", { name: "Public" }).click();
  await expectSearch(page, { nameContains: prefix, isPublic: "true" });
  await expect(rowNames(page)).toHaveText([`${prefix}-public`]);

  await page.reload();
  await expect(visibility).toHaveText("Public");
  await expect(rowNames(page)).toHaveText([`${prefix}-public`]);
});

test("filters the repository list by owner, as (you) and as another user, and finds the filter again on reload", async ({
  page,
  browser,
}) => {
  const prefix = uniqueName("filter-owner");

  // `other` signs in in a context of their own, so that they exist and own something here, whatever
  // other tests have done.
  const otherContext = await browser.newContext();
  try {
    const otherPage = await otherContext.newPage();
    await signIn(otherPage, users.other);
    // Public, so that the test user can see it.
    await createRepository(otherPage, `${prefix}-theirs`, { isPublic: true });
  } finally {
    await otherContext.close();
  }

  await signIn(page, users.test);
  await createRepository(page, `${prefix}-mine`);
  await gotoList(page, prefix);
  await expect(rowNames(page)).toHaveText([
    `${prefix}-mine`,
    `${prefix}-theirs`,
  ]);

  const owner = page.getByRole("main").getByRole("combobox", { name: "Owner" });
  await owner.click();
  await page.getByRole("option", { name: /\(you\)$/ }).click();
  await expect(owner).toHaveValue(/\(you\)$/);
  await expect
    .poll(() => searchParams(page).get("ownerId"))
    .toMatch(/^[0-9a-f-]{36}$/);
  await expect(rowNames(page)).toHaveText([`${prefix}-mine`]);

  await owner.click();
  await owner.fill("oth");
  await page.getByRole("option", { name: "other", exact: true }).click();
  await expect(owner).toHaveValue("other");
  await expect(rowNames(page)).toHaveText([`${prefix}-theirs`]);
  const otherId = searchParams(page).get("ownerId");
  expect(otherId).not.toBeNull();

  await page.reload();
  await expect(owner).toHaveValue("other");
  expect(searchParams(page).get("ownerId")).toBe(otherId);
  await expect(rowNames(page)).toHaveText([`${prefix}-theirs`]);
});

test("sorts the repository list by each column in both directions, and finds the sort again on reload", async ({
  page,
}) => {
  await signIn(page, users.test);
  const prefix = uniqueName("sort");
  // Created in this order, so that the creation order differs from the names'.
  for (const suffix of ["b", "c", "a"]) {
    await createRepository(page, `${prefix}-${suffix}`);
  }
  const inOrder = (...suffixes: string[]) =>
    suffixes.map((suffix) => `${prefix}-${suffix}`);
  const table = repositoryTable(page);
  const header = (name: string) => table.getByRole("columnheader", { name });

  await gotoList(page, prefix);
  await expect(rowNames(page)).toHaveText(inOrder("a", "b", "c"));
  await expect(header("Name")).toHaveAttribute("aria-sort", "ascending");

  await header("Name").getByRole("button").click();
  await expectSearch(page, {
    nameContains: prefix,
    sortBy: "Name",
    direction: "Descending",
  });
  await expect(rowNames(page)).toHaveText(inOrder("c", "b", "a"));
  await expect(header("Name")).toHaveAttribute("aria-sort", "descending");

  for (const column of ["Created", "Updated"]) {
    // Newest first is each date's default direction.
    await header(column).getByRole("button").click();
    await expectSearch(page, { nameContains: prefix, sortBy: column });
    await expect(rowNames(page)).toHaveText(inOrder("a", "c", "b"));
    await expect(header(column)).toHaveAttribute("aria-sort", "descending");

    await header(column).getByRole("button").click();
    await expectSearch(page, {
      nameContains: prefix,
      sortBy: column,
      direction: "Ascending",
    });
    await expect(rowNames(page)).toHaveText(inOrder("b", "c", "a"));
    await expect(header(column)).toHaveAttribute("aria-sort", "ascending");
  }

  await page.reload();
  await expect(rowNames(page)).toHaveText(inOrder("b", "c", "a"));
  await expect(header("Updated")).toHaveAttribute("aria-sort", "ascending");
});

test("pages through the repository list, finds the page again on reload, and starts again at the first page on a sort", async ({
  page,
}) => {
  await signIn(page, users.test);
  const prefix = uniqueName("paging");
  const headers = await antiforgeryHeaders(page);
  const names = Array.from(
    { length: 26 },
    (_, index) => `${prefix}-${String(index).padStart(2, "0")}`,
  );
  await Promise.all(
    names.map(async (name) => {
      const created = await page.request.post(
        "/api/pacmanmanager/v1/Repositories",
        { headers, data: { name, isPublic: false } },
      );
      expect(created.status(), await created.text()).toBe(201);
    }),
  );

  await gotoList(page, prefix, { pageSize: "25" });
  const main = page.getByRole("main");
  await expect(main.getByText("1–25 of 26")).toBeVisible();
  await expect(rowNames(page)).toHaveCount(25);
  await expect(rowNames(page).first()).toHaveText(names[0]!);

  await main.getByRole("button", { name: "Go to next page" }).click();
  await expect(main.getByText("26–26 of 26")).toBeVisible();
  await expect(rowNames(page)).toHaveText([names[25]!]);
  await expectSearch(page, {
    nameContains: prefix,
    pageSize: "25",
    offset: "25",
  });

  await page.reload();
  await expect(main.getByText("26–26 of 26")).toBeVisible();
  await expect(rowNames(page)).toHaveText([names[25]!]);

  await repositoryTable(page)
    .getByRole("columnheader", { name: "Name" })
    .getByRole("button")
    .click();
  await expectSearch(page, {
    nameContains: prefix,
    pageSize: "25",
    sortBy: "Name",
    direction: "Descending",
  });
  await expect(main.getByText("1–25 of 26")).toBeVisible();
  await expect(rowNames(page)).toHaveCount(25);
  await expect(rowNames(page).first()).toHaveText(names[25]!);
});
