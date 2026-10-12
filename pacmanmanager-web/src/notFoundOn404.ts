import { notFound } from "@tanstack/react-router";
import { ApiError } from "./api";

/**
 * A route's `onError`: turns an `ApiError` with status `404` from its `beforeLoad` or loader into
 * the router's `notFound()`, so the route's not-found component is shown rather than the error
 * component. Any other error is left to the error component.
 *
 * @example
 * export const Route = createFileRoute("/repositories/$id")({
 *   loader: ({ context, params }) =>
 *     context.queryClient.ensureQueryData(repositoryQuery(params.id)),
 *   onError: notFoundOn404,
 * });
 */
export function notFoundOn404(error: unknown): void {
  if (error instanceof ApiError && error.status === 404) {
    throw notFound();
  }
}
