import {
  antiforgeryHeaders,
  expect,
  signIn,
  test,
  uniqueName,
  users,
} from "./fixtures.ts";
import { buildPackage } from "./packages.ts";

/** Kestrel's default request body limit, which the web host's proxy is held to unless raised. */
const kestrelDefaultLimit = 30 * 1024 * 1024;

test("a signed-in user publishes a package over 30 MB through the web host", async ({
  page,
}) => {
  test.setTimeout(120_000);
  await signIn(page, users.test);

  const created = await page.request.post(
    "/api/pacmanmanager/v1/Repositories",
    {
      headers: await antiforgeryHeaders(page),
      data: { name: uniqueName("large-upload"), isPublic: false },
    },
  );
  expect(created.status(), await created.text()).toBe(201);
  const repository = (await created.json()) as { id: string };

  const pkg = buildPackage({
    name: "pacmanmanager-e2e-large",
    version: "1.0.0-1",
    payloadBytes: kestrelDefaultLimit + 8 * 1024 * 1024,
  });
  expect(pkg.length).toBeGreaterThan(kestrelDefaultLimit);

  const published = await page.request.post(
    `/api/pacmanmanager/v1/repositories/${repository.id}/packages`,
    {
      headers: {
        ...(await antiforgeryHeaders(page)),
        "Content-Type": "application/octet-stream",
      },
      data: pkg,
      timeout: 90_000,
    },
  );

  expect(published.status(), await published.text()).toBe(201);
  expect(await published.json()).toMatchObject({
    name: "pacmanmanager-e2e-large",
    version: "1.0.0-1",
    repositoryId: repository.id,
    compressedSize: pkg.length,
  });
});
