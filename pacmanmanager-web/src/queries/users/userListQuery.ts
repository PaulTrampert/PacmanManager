import { queryOptions } from "@tanstack/react-query";
import { client, unwrap, type paths } from "../../api";

/**
 * What `GET /v1/Users` may be asked for: paging (`offset`, `pageSize`), the `displayNameContains`
 * filter and the sort (`sortBy`, `direction`), named as the API names them.
 */
export type UserListSearch = NonNullable<
  paths["/v1/Users"]["get"]["parameters"]["query"]
>;

/**
 * The query options for a page of users. The owner filter searches with it
 * (`{ displayNameContains, pageSize: 20 }`), and the list route counts the users with it
 * (`{ pageSize: 1 }`, reading `total`). Every parameter in `search` is sent and no other; the key
 * holds them all.
 */
export function userListQuery(search: UserListSearch = {}) {
  return queryOptions({
    queryKey: ["users", "list", search],
    queryFn: ({ signal }) =>
      unwrap(client.GET("/v1/Users", { params: { query: search }, signal })),
  });
}
