import { queryOptions } from "@tanstack/react-query";
import { client, unwrap, type paths } from "../../api";

/**
 * What a repository list query is asked for: the filter, sort and paging parameters of
 * `GET /v1/Repositories`, under the API's own names. A parameter that is absent is not sent.
 */
export type RepositoryListSearch = NonNullable<
  paths["/v1/Repositories"]["get"]["parameters"]["query"]
>;

/**
 * The key every repository list query's key starts with. A mutation that changes a repository
 * invalidates the list queries by it.
 */
export const repositoryListKey = ["repositories", "list"] as const;

/**
 * The query options for one page of `GET /v1/Repositories`. It sends exactly the parameters in
 * `search`, and no others: defaults are left to the API, and which filters apply is decided by the
 * caller. The key holds `search`, so each distinct request is cached apart.
 */
export function repositoryListQuery(search: RepositoryListSearch) {
  return queryOptions({
    queryKey: [...repositoryListKey, search] as const,
    queryFn: ({ signal }) =>
      unwrap(
        client.GET("/v1/Repositories", { params: { query: search }, signal }),
      ),
  });
}
