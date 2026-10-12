import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import type { Page } from "@playwright/test";
import {
  antiforgeryCookie,
  antiforgeryHeader,
  antiforgeryPath,
} from "../src/api/antiforgery.ts";
import { expect, signIn, test, users } from "./fixtures.ts";

const packageFileName = "pacmanmanager-test-1.2.3-4-x86_64.pkg.tar.zst";

/** The minimal package fixture shared with the .NET tests; see test-fixtures/packages/README.md. */
const packagePath = fileURLToPath(
  new URL(`../../test-fixtures/packages/${packageFileName}`, import.meta.url),
);

/** RepoHost's API as the web host proxies it. */
const api = "/api/pacmanmanager/v1";

/** Fetches an antiforgery token for `page`'s cookie session, as the app's API client does. */
async function antiforgeryToken(page: Page): Promise<string> {
  const response = await page.request.get(antiforgeryPath);
  expect(response.ok()).toBe(true);
  const cookie = (await page.context().cookies()).find(
    (c) => c.name === antiforgeryCookie,
  );
  expect(cookie, `${antiforgeryPath} sets ${antiforgeryCookie}`).toBeDefined();
  return decodeURIComponent(cookie!.value);
}

test("a package downloaded through the web host is the uploaded file, saved under its own name", async ({
  page,
}) => {
  const upload = await readFile(packagePath);
  await signIn(page, users.test);

  const created = await page.request.post(`${api}/Repositories`, {
    headers: { [antiforgeryHeader]: await antiforgeryToken(page) },
    data: { name: `e2e-download-${crypto.randomUUID().slice(0, 8)}` },
  });
  expect(created.status()).toBe(201);
  const repository = (await created.json()) as { id: string };

  const published = await page.request.post(
    `${api}/repositories/${repository.id}/packages`,
    {
      headers: {
        [antiforgeryHeader]: await antiforgeryToken(page),
        "Content-Type": "application/octet-stream",
      },
      data: upload,
    },
  );
  expect(published.status()).toBe(201);
  const pkg = (await published.json()) as { id: string };

  // A real browser download, so the filename is the one Chromium takes from Content-Disposition.
  const downloading = page.waitForEvent("download");
  await page.evaluate((href) => {
    const link = document.createElement("a");
    link.href = href;
    document.body.append(link);
    link.click();
    link.remove();
  }, `${api}/Packages/${pkg.id}/content`);
  const download = await downloading;

  expect(download.suggestedFilename()).toBe(packageFileName);
  const downloaded = await readFile(await download.path());
  expect(downloaded.equals(upload)).toBe(true);
});
