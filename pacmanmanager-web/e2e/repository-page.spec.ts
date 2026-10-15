import {
  antiforgeryHeaders,
  expect,
  signIn,
  test,
  uniqueName,
  users,
} from "./fixtures.ts";

/** RepoHost's API as the web host proxies it. */
const api = "/api/pacmanmanager/v1";

test("a repository opened from the list shows its pacman.conf section once expanded", async ({
  page,
}) => {
  await signIn(page, users.test);
  const name = uniqueName("e2e-repository-page");
  const created = await page.request.post(`${api}/Repositories`, {
    headers: await antiforgeryHeaders(page),
    data: { name },
  });
  expect(created.status()).toBe(201);
  const repository = (await created.json()) as {
    id: string;
    serverUrl: string;
  };

  // Filtered to the repository's name, since every test shares one database.
  await page.goto(
    `/repositories?${new URLSearchParams({ nameContains: name })}`,
  );
  await page.getByRole("link", { name, exact: true }).click();

  await expect(page).toHaveURL(`/repositories/${repository.id}`);
  const main = page.getByRole("main");
  await expect(main.getByRole("heading", { level: 1, name })).toBeVisible();

  const section = main.getByRole("button", { name: /Use this repository/ });
  await expect(section).toHaveAttribute("aria-expanded", "false");
  await section.click();

  await expect(main.locator("pre")).toHaveText(
    `[${name}]\nSigLevel = Optional TrustAll\nServer = ${repository.serverUrl}`,
  );
});

test("a repository deleted by typing its name is gone from the list, and its page is not found", async ({
  page,
}) => {
  await signIn(page, users.test);
  const name = uniqueName("e2e-delete");
  const created = await page.request.post(`${api}/Repositories`, {
    headers: await antiforgeryHeaders(page),
    data: { name },
  });
  expect(created.status()).toBe(201);
  const repository = (await created.json()) as { id: string };

  await page.goto(`/repositories/${repository.id}`);
  const main = page.getByRole("main");
  await expect(main.getByRole("heading", { level: 1, name })).toBeVisible();

  await main.getByRole("button", { name: "Delete" }).click();
  const dialog = page.getByRole("dialog", { name: `Delete ${name}?` });
  const remove = dialog.getByRole("button", { name: "Delete" });
  await expect(remove).toBeDisabled();
  await dialog
    .getByRole("textbox", { name: `Type ${name} to confirm` })
    .fill(name);
  await remove.click();

  await expect(page).toHaveURL(/\/repositories$/);
  await expect(
    main.getByRole("heading", { level: 1, name: "Repositories" }),
  ).toBeVisible();

  // Filtered to the repository's name, since every test shares one database.
  await page.goto(
    `/repositories?${new URLSearchParams({ nameContains: name })}`,
  );
  await expect(
    main.getByRole("heading", { name: "No repositories match" }),
  ).toBeVisible();
  await expect(main.getByRole("link", { name, exact: true })).toHaveCount(0);

  await page.goto(`/repositories/${repository.id}`);
  await expect(
    main.getByRole("heading", { level: 1, name: "Page not found" }),
  ).toBeVisible();
});
