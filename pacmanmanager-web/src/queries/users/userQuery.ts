import { queryOptions } from "@tanstack/react-query";
import { client, unwrap } from "../../api";

/**
 * The query options for one user's public record, `GET /v1/Users/{userId}`. The repository list
 * uses it to label an `ownerId` taken from the URL with that user's display name.
 *
 * A user who does not exist is a `404`, thrown as an `ApiError`, which is never retried.
 *
 * @param id The user's id.
 */
export function userQuery(id: string) {
  return queryOptions({
    queryKey: ["users", "detail", id],
    queryFn: () =>
      unwrap(
        client.GET("/v1/Users/{userId}", { params: { path: { userId: id } } }),
      ),
  });
}
