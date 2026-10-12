import { waitUntilReady } from "./readiness.ts";
import { readinessUrls } from "./stack.ts";

/**
 * Playwright's global setup. It never starts the stack — `npm run e2e`'s `pree2e` script does that —
 * it only waits for the stack to answer, so the first test does not race Keycloak's start-up.
 */
export default async function globalSetup(): Promise<void> {
  await waitUntilReady(readinessUrls, {
    timeoutMs: 120_000,
    intervalMs: 1_000,
  });
}
