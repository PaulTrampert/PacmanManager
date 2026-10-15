import {
  antiforgeryHeaders,
  expect,
  signIn,
  test,
  uniqueName,
  users,
} from "./fixtures.ts";

test("a signed-in user sees a repository they created in the repository list", async ({
  page,
}) => {
  await signIn(page, users.test);
  const name = uniqueName("list-page");
  const created = await page.request.post(
    "/api/pacmanmanager/v1/Repositories",
    {
      headers: await antiforgeryHeaders(page),
      data: { name, isPublic: false },
    },
  );
  expect(created.status(), await created.text()).toBe(201);
  const repository = (await created.json()) as { id: string };

  // Other tests share the database, so the list is narrowed to this test's repository by its URL.
  await page.goto(
    `/repositories?${new URLSearchParams({ nameContains: name })}`,
  );

  const main = page.getByRole("main");
  await expect(
    main.getByRole("heading", { level: 1, name: "Repositories" }),
  ).toBeVisible();
  await expect(
    main.getByRole("link", { name: "New repository" }),
  ).toBeVisible();
  await expect(main.getByRole("textbox", { name: "Name" })).toHaveValue(name);
  const table = main.getByRole("table", { name: "Repositories" });
  const row = table.getByRole("row").filter({ hasText: name });
  await expect(row).toHaveCount(1);
  await expect(row.getByRole("link", { name })).toHaveAttribute(
    "href",
    `/repositories/${repository.id}`,
  );
  await expect(row.getByText("Private")).toBeVisible();
});
