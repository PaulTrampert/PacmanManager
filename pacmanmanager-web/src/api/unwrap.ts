import { ApiError } from "./ApiError";

/** The shape of an `openapi-fetch` result: its data or its error, and the response either way. */
export interface ClientResult<T> {
  data?: T;
  error?: unknown;
  response: Response;
}

/**
 * Awaits an API client call and returns its data, or throws an {@link ApiError} carrying the
 * status and the problem details of an error response. Query functions and mutations call the
 * client through this, so nothing above them handles an `openapi-fetch` result.
 *
 * @example
 * const user = await unwrap(client.GET("/v1/Users/me"));
 */
export async function unwrap<T>(pending: Promise<ClientResult<T>>): Promise<T> {
  const { data, error, response } = await pending;
  if (!response.ok) {
    throw new ApiError(response.status, error);
  }
  return data as T;
}
