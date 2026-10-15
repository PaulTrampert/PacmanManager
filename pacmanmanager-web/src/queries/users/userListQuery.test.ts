import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { ApiError } from "../../api";
import { apiHandler, toMockPath } from "../../mocks/api";
import { aPage, aUser } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { createQueryClient } from "../../queryClient";
import { userListQuery } from "./userListQuery";

/** Answers `GET /v1/Users` with `page`, recording the query string of each request it answers. */
function recordUserListRequests(page = aPage([aUser()])): URLSearchParams[] {
  const requests: URLSearchParams[] = [];
  server.use(
    apiHandler("get", "/v1/Users", ({ request }) => {
      requests.push(new URL(request.url).searchParams);
      return HttpResponse.json(page);
    }),
  );
  return requests;
}

describe("userListQuery", () => {
  it("sends the owner search's text and page size, and nothing else", async () => {
    const requests = recordUserListRequests();

    await createQueryClient().fetchQuery(
      userListQuery({ displayNameContains: "ada", pageSize: 20 }),
    );

    expect(requests).toHaveLength(1);
    expect([...requests[0]!].sort()).toEqual([
      ["displayNameContains", "ada"],
      ["pageSize", "20"],
    ]);
  });

  it("sends only pageSize=1 for the user count, and resolves to the page reporting the total", async () => {
    const page = aPage([aUser()], { total: 7 });
    const requests = recordUserListRequests(page);

    const result = await createQueryClient().fetchQuery(
      userListQuery({ pageSize: 1 }),
    );

    expect([...requests[0]!]).toEqual([["pageSize", "1"]]);
    expect(result).toEqual(page);
    expect(result.total).toBe(7);
  });

  it("sends every paging and sort parameter it is given", async () => {
    const requests = recordUserListRequests();

    await createQueryClient().fetchQuery(
      userListQuery({
        offset: 40,
        pageSize: 20,
        displayNameContains: "lovelace",
        sortBy: "DisplayName",
        direction: "Descending",
      }),
    );

    expect(Object.fromEntries(requests[0]!)).toEqual({
      offset: "40",
      pageSize: "20",
      displayNameContains: "lovelace",
      sortBy: "DisplayName",
      direction: "Descending",
    });
  });

  it("sends no parameters when given none", async () => {
    const requests = recordUserListRequests();

    await createQueryClient().fetchQuery(userListQuery());

    expect([...requests[0]!]).toEqual([]);
  });

  it("keys the query by the users resource and every parameter it sends", () => {
    expect(
      userListQuery({ displayNameContains: "ada", pageSize: 20 }).queryKey,
    ).toEqual(["users", "list", { displayNameContains: "ada", pageSize: 20 }]);
  });

  it("gives the owner search and the user count separate cache entries", async () => {
    const requests = recordUserListRequests();
    const queryClient = createQueryClient();

    await queryClient.fetchQuery(userListQuery({ pageSize: 1 }));
    await queryClient.fetchQuery(
      userListQuery({ displayNameContains: "ada", pageSize: 20 }),
    );
    await queryClient.fetchQuery(userListQuery({ pageSize: 1 }));

    expect(requests).toHaveLength(2);
  });

  it("rejects with an ApiError when the API refuses the request", async () => {
    // The document declares no error response for this operation, so the typed handler cannot
    // answer with one.
    server.use(
      http.get(toMockPath("/v1/Users"), () =>
        HttpResponse.json({ title: "Bad Request" }, { status: 400 }),
      ),
    );

    const error = await createQueryClient()
      .fetchQuery(userListQuery({ pageSize: 1 }))
      .catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
  });
});
