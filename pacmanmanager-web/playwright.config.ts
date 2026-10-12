import { defineConfig, devices } from "@playwright/test";
import { webHostUrl } from "./e2e/stack.ts";

const ci = !!process.env.CI;

// End-to-end tests against the compose stack, which `npm run e2e` starts first through its `pree2e`
// script; global setup only waits for it. See docs/web-ui-architecture.md, "End-to-end tests".
export default defineConfig({
  testDir: "e2e",
  // `*.test.ts` under e2e/ are Vitest unit tests of the helpers, not Playwright tests.
  testMatch: "**/*.spec.ts",
  globalSetup: "./e2e/global-setup.ts",
  forbidOnly: ci,
  retries: ci ? 1 : 0,
  reporter: ci ? [["github"], ["html", { open: "never" }]] : "html",
  use: {
    // The built image in the compose stack, never the dev server.
    baseURL: webHostUrl,
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
