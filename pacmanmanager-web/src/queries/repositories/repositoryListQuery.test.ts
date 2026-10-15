import { QueryClient } from "@tanstack/react-query";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { ApiError } from "../../api";
import { apiHandler, toMockPath } from "../../mocks/api";
import { aPage, aRepository } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { repositoryKeys } from "./repositoryKeys";
import {
  repositoryListQuery,
  type RepositoryListSearch,
} from "./repositoryListQuery";

/** Answers the list with `page`, recording the query string of each request it answers. */
function listHandler(page = aPage([aRepository()])) {
  const requests: URLSearchParams[] = [];
  server.use(
    apiHandler("get", "/v1/Repositories", ({ request }) => {
      requests.push(new URL(request.url).searchParams);
      return HttpResponse.json(page);
    }),
  );
  return requests;
}

function fetchList(search: RepositoryListSearch) {
  return new QueryClient().fetchQuery(repositoryListQuery(search));
}

describe("repositoryListQuery", () => {
  it("sends every filter, sort and paging parameter it is given", async () => {
    const requests = listHandler();

    await fetchList({
      nameContains: "core",
      isPublic: false,
      ownerId: "6f1c1f0e-8a52-4a3b-9a57-2c5d3e8f9b10",
      architecture: "aarch64",
      sortBy: "Updated",
      direction: "Ascending",
      offset: 100,
      pageSize: 25,
    });

    expect(requests).toHaveLength(1);
    expect(Object.fromEntries(requests[0]!)).toEqual({
      nameContains: "core",
      isPublic: "false",
      ownerId: "6f1c1f0e-8a52-4a3b-9a57-2c5d3e8f9b10",
      architecture: "aarch64",
      sortBy: "Updated",
      direction: "Ascending",
      offset: "100",
      pageSize: "25",
    });
  });

  it("sends no parameter it is not given", async () => {
    const requests = listHandler();

    await fetchList({});

    expect([...requests[0]!.keys()]).toEqual([]);
  });

  it("sends only the parameters it is given, leaving the rest to the API's defaults", async () => {
    const requests = listHandler();

    await fetchList({ nameContains: "core", offset: 0, direction: undefined });

    expect(Object.fromEntries(requests[0]!)).toEqual({
      nameContains: "core",
      offset: "0",
    });
  });

  it("returns the page the API answers with", async () => {
    const page = aPage([aRepository({ name: "core" })], { total: 7 });
    listHandler(page);

    await expect(fetchList({})).resolves.toEqual(page);
  });

  it("throws an ApiError when the API fails", async () => {
    // The document declares no error response for the list, so the handler is untyped.
    server.use(
      http.get(
        toMockPath("/v1/Repositories"),
        () => new HttpResponse(null, { status: 500 }),
      ),
    );

    const error = await fetchList({ pageSize: 25 }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(500);
  });

  it("keys the query by the list key and the search it sends", () => {
    const search: RepositoryListSearch = { nameContains: "core", offset: 50 };

    expect(repositoryListQuery(search).queryKey).toEqual([
      ...repositoryKeys.lists(),
      search,
    ]);
  });
});
