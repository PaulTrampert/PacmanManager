import { composeUpCommand } from "./stack.ts";

/** How {@link waitUntilReady} polls. */
export interface ReadinessOptions {
  /** How long to keep trying before giving up, in milliseconds. */
  timeoutMs: number;
  /** How long to wait between rounds of requests, in milliseconds. */
  intervalMs: number;
}

/** Thrown by {@link waitUntilReady} when some address has not answered within the timeout. */
export class StackNotReadyError extends Error {
  /** The addresses that had not answered with a success status when time ran out. */
  readonly pending: readonly string[];

  constructor(pending: readonly string[], timeoutMs: number) {
    super(
      `The compose stack did not become ready within ${timeoutMs / 1000} seconds. ` +
        `Still waiting for: ${pending.join(", ")}. ` +
        `Start it from the repository root with \`${composeUpCommand}\` ` +
        `(\`npm run e2e\` does this first), and check \`docker compose logs\` if it is running.`,
    );
    this.name = "StackNotReadyError";
    this.pending = pending;
  }
}

async function answers(url: string, timeoutMs: number): Promise<boolean> {
  try {
    const response = await fetch(url, {
      signal: AbortSignal.timeout(timeoutMs),
    });
    return response.ok;
  } catch {
    // Refused, reset or timed out: the service is not up yet.
    return false;
  }
}

/**
 * Resolves once every one of `urls` has answered a `GET` with a success status, and rejects with a
 * {@link StackNotReadyError} naming the compose command if any has not within the timeout. An
 * address that has answered once is not asked again.
 */
export async function waitUntilReady(
  urls: readonly string[],
  { timeoutMs, intervalMs }: ReadinessOptions,
): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  let pending = [...urls];
  for (;;) {
    const remaining = Math.max(deadline - Date.now(), 1);
    const results = await Promise.all(
      pending.map((url) => answers(url, remaining)),
    );
    pending = pending.filter((_, index) => !results[index]);
    if (pending.length === 0) {
      return;
    }
    if (Date.now() + intervalMs >= deadline) {
      throw new StackNotReadyError(pending, timeoutMs);
    }
    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }
}
