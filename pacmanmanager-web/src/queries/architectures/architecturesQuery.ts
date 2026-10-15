import { queryOptions } from "@tanstack/react-query";
import { client, unwrap } from "../../api";

/**
 * How long the architectures stay fresh, in milliseconds: an hour. The list is static, defined by
 * the server rather than by anything a user does, so it is fetched once and refetched only after
 * that long, far beyond the default stale time.
 */
export const architecturesStaleTime = 60 * 60 * 1000;

/**
 * The query options for the architectures a repository may support, as `GET /v1/Architectures`
 * lists them: a plain `{ value, displayValue }` array in the server's order.
 */
export function architecturesQuery() {
  return queryOptions({
    queryKey: ["architectures"],
    queryFn: () => unwrap(client.GET("/v1/Architectures")),
    staleTime: architecturesStaleTime,
  });
}
