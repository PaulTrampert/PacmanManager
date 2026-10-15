import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import { createElement } from "react";
import { describe, expect, it } from "vitest";
import { ApiError, type WriteRepositoryRequest } from "../../api";
import { apiHandler } from "../../mocks/api";
import { aPage, aRepository } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { createQueryClient } from "../../queryClient";
import { repositoryKeys } from "./repositoryKeys";
import { useUpdateRepository } from "./useUpdateRepository";

const updated = aRepository({ id: "11111111-1111-4111-8111-111111111111" });
const other = aRepository({
  id: "22222222-2222-4222-8222-222222222222",
  name: "other-repo",
});
const listKey = [...repositoryKeys.lists(), { name: "repo" }];
const request: WriteRepositoryRequest = {
  name: "renamed-repo",
  supportedArchitectures: ["x86_64", "aarch64"],
  isPublic: false,
};
const saved = aRepository({
  ...updated,
  ...request,
  updatedAt: "2026-02-01T00:00:00Z",
});

/**
 * A query client holding a repository list and the detail queries of `updated` and `other`, none
 * of them observed, so nothing refetches them.
 */
function seededQueryClient(): QueryClient {
  const queryClient = createQueryClient();
  queryClient.setQueryData(listKey, aPage([updated, other]));
  queryClient.setQueryData(repositoryKeys.detail(updated.id), updated);
  queryClient.setQueryData(repositoryKeys.detail(other.id), other);
  return queryClient;
}

function renderUpdateRepository(queryClient: QueryClient) {
  return renderHook(() => useUpdateRepository(), {
    wrapper: ({ children }) =>
      createElement(QueryClientProvider, { client: queryClient }, children),
  });
}

/**
 * Answers the update with `200` and {@link saved}, recording the id and body of each request it
 * receives.
 */
function answerUpdate() {
  const requests: { id: string; body: unknown }[] = [];
  server.use(
    apiHandler("put", "/v1/Repositories/{id}", async ({ params, request }) => {
      requests.push({ id: params.id, body: await request.json() });
      return HttpResponse.json(saved);
    }),
  );
  return requests;
}

describe("useUpdateRepository", () => {
  it("sends PUT /v1/Repositories/{id} with the request it is given", async () => {
    const requests = answerUpdate();
    const { result } = renderUpdateRepository(seededQueryClient());

    await act(() => result.current.mutateAsync({ id: updated.id, request }));

    expect(requests).toEqual([{ id: updated.id, body: request }]);
  });

  it("resolves to the repository the API returns", async () => {
    answerUpdate();
    const { result } = renderUpdateRepository(seededQueryClient());

    const repository = await act(() =>
      result.current.mutateAsync({ id: updated.id, request }),
    );

    expect(repository).toEqual(saved);
  });

  it("invalidates the lists and sets only the updated repository's detail query to the response", async () => {
    answerUpdate();
    const queryClient = seededQueryClient();
    const { result } = renderUpdateRepository(queryClient);

    await act(() => result.current.mutateAsync({ id: updated.id, request }));

    expect(queryClient.getQueryState(listKey)?.isInvalidated).toBe(true);
    expect(queryClient.getQueryData(repositoryKeys.detail(updated.id))).toEqual(
      saved,
    );
    expect(
      queryClient.getQueryState(repositoryKeys.detail(updated.id))
        ?.isInvalidated,
    ).toBe(false);
    expect(queryClient.getQueryData(repositoryKeys.detail(other.id))).toEqual(
      other,
    );
    expect(
      queryClient.getQueryState(repositoryKeys.detail(other.id))?.isInvalidated,
    ).toBe(false);
  });

  it("changes nothing in the cache when the update fails", async () => {
    server.use(
      apiHandler("put", "/v1/Repositories/{id}", () =>
        HttpResponse.json(
          { title: "Conflict", status: 409, detail: "The name is taken." },
          { status: 409 },
        ),
      ),
    );
    const queryClient = seededQueryClient();
    const { result } = renderUpdateRepository(queryClient);

    act(() => result.current.mutate({ id: updated.id, request }));

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ApiError);
    expect(result.current.error).toMatchObject({ status: 409 });
    expect(queryClient.getQueryState(listKey)?.isInvalidated).toBe(false);
    expect(queryClient.getQueryData(repositoryKeys.detail(updated.id))).toEqual(
      updated,
    );
  });
});
