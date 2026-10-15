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
import { useCreateRepository } from "./useCreateRepository";

const existing = aRepository({ id: "11111111-1111-4111-8111-111111111111" });
const created = aRepository({
  id: "22222222-2222-4222-8222-222222222222",
  name: "custom-repo",
  isPublic: false,
  supportedArchitectures: ["x86_64"],
});
const request: WriteRepositoryRequest = {
  name: "custom-repo",
  supportedArchitectures: ["x86_64"],
  isPublic: false,
};
const listKey = [...repositoryKeys.lists(), { nameContains: "repo" }];

/**
 * A query client holding a repository list and `existing`'s detail query, neither of them
 * observed, so nothing refetches them.
 */
function seededQueryClient(): QueryClient {
  const queryClient = createQueryClient();
  queryClient.setQueryData(listKey, aPage([existing]));
  queryClient.setQueryData(repositoryKeys.detail(existing.id), existing);
  return queryClient;
}

function renderCreateRepository(queryClient: QueryClient) {
  return renderHook(() => useCreateRepository(), {
    wrapper: ({ children }) =>
      createElement(QueryClientProvider, { client: queryClient }, children),
  });
}

/** Answers the create with `201` and `created`, recording the body of each request it answers. */
function answerCreate() {
  const bodies: unknown[] = [];
  server.use(
    apiHandler("post", "/v1/Repositories", async ({ request }) => {
      bodies.push(await request.json());
      return HttpResponse.json(created, { status: 201 });
    }),
  );
  return bodies;
}

describe("useCreateRepository", () => {
  it("sends POST /v1/Repositories with the request it is given", async () => {
    const bodies = answerCreate();
    const { result } = renderCreateRepository(seededQueryClient());

    await act(() => result.current.mutateAsync(request));

    expect(bodies).toEqual([request]);
  });

  it("resolves to the created repository", async () => {
    answerCreate();
    const { result } = renderCreateRepository(seededQueryClient());

    const data = await act(() => result.current.mutateAsync(request));

    expect(data).toEqual(created);
  });

  it("invalidates the lists and leaves the detail queries alone", async () => {
    answerCreate();
    const queryClient = seededQueryClient();
    const { result } = renderCreateRepository(queryClient);

    await act(() => result.current.mutateAsync(request));

    expect(queryClient.getQueryState(listKey)?.isInvalidated).toBe(true);
    expect(
      queryClient.getQueryState(repositoryKeys.detail(existing.id))
        ?.isInvalidated,
    ).toBe(false);
    expect(
      queryClient.getQueryCache().find({
        queryKey: repositoryKeys.detail(created.id),
      }),
    ).toBeUndefined();
  });

  it("fails with an ApiError and changes nothing in the cache when the create fails", async () => {
    server.use(
      apiHandler("post", "/v1/Repositories", () =>
        HttpResponse.json({ title: "Conflict", status: 409 }, { status: 409 }),
      ),
    );
    const queryClient = seededQueryClient();
    const { result } = renderCreateRepository(queryClient);

    act(() => result.current.mutate(request));

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ApiError);
    expect(result.current.error).toMatchObject({ status: 409 });
    expect(queryClient.getQueryState(listKey)?.isInvalidated).toBe(false);
  });
});
