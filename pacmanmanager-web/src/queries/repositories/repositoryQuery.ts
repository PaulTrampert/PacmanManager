import { queryOptions } from "@tanstack/react-query";
import { client, unwrap } from "../../api";
import { repositoryKeys } from "./repositoryKeys";

/**
 * One repository's query options, by id, over `GET /v1/Repositories/{id}`. A repository the caller
 * may not see, or that does not exist, fails with an `ApiError` of status `404`, which the route
 * turns into its not-found component with `notFoundOn404`.
 *
 * @example
 * loader: ({ context, params }) =>
 *   context.queryClient.ensureQueryData(repositoryQuery(params.repositoryId)),
 */
export function repositoryQuery(id: string) {
  return queryOptions({
    queryKey: repositoryKeys.detail(id),
    queryFn: ({ signal }) =>
      unwrap(
        client.GET("/v1/Repositories/{id}", {
          params: { path: { id } },
          signal,
        }),
      ),
  });
}
