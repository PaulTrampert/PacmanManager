import { QueryClient } from "@tanstack/react-query";

/** How many times a failed query is retried when the failure may be transient. */
export const maxQueryRetries = 2;

/** How long fetched data counts as fresh, in milliseconds. */
export const queryStaleTime = 30_000;

/**
 * Whether `error` is the server's considered answer to the request: an error carrying an HTTP
 * status in the 4xx range, as the API client's `ApiError` does. Asking again gets the same answer,
 * so such a failure is never retried.
 */
export function isClientError(error: unknown): boolean {
  if (typeof error !== "object" || error === null || !("status" in error)) {
    return false;
  }
  const { status } = error;
  return typeof status === "number" && status >= 400 && status < 500;
}

/**
 * The retry rule for queries: never for a client error, otherwise up to {@link maxQueryRetries}
 * times. `failureCount` is the number of retries already made.
 */
export function shouldRetryQuery(
  failureCount: number,
  error: unknown,
): boolean {
  return !isClientError(error) && failureCount < maxQueryRetries;
}

/** Creates the `QueryClient` with the app's defaults. */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: shouldRetryQuery,
        staleTime: queryStaleTime,
        refetchOnWindowFocus: true,
      },
      mutations: {
        // A retried POST can do its work twice.
        retry: false,
      },
    },
  });
}
