import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import { createElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api";
import { apiHandler } from "../../mocks/api";
import { aPage, aRepository } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { createQueryClient } from "../../queryClient";
import { repositoryKeys } from "./repositoryKeys";
import {
  useDeleteRepository,
  type UseDeleteRepositoryOptions,
} from "./useDeleteRepository";

const deleted = aRepository({ id: "11111111-1111-4111-8111-111111111111" });
const other = aRepository({
  id: "22222222-2222-4222-8222-222222222222",
  name: "other-repo",
});
const listKey = [...repositoryKeys.lists(), { name: "repo" }];

/** A promise and the functions that settle it, for holding `onDeleted` open. */
function deferred() {
  let resolve!: () => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<void>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

/**
 * A query client holding a repository list and the detail queries of `deleted` and `other`, none
 * of them observed, so nothing refetches them.
 */
function seededQueryClient(): QueryClient {
  const queryClient = createQueryClient();
  queryClient.setQueryData(listKey, aPage([deleted, other]));
  queryClient.setQueryData(repositoryKeys.detail(deleted.id), deleted);
  queryClient.setQueryData(repositoryKeys.detail(other.id), other);
  return queryClient;
}

function renderDeleteRepository(
  queryClient: QueryClient,
  options?: UseDeleteRepositoryOptions,
) {
  return renderHook(() => useDeleteRepository(options), {
    wrapper: ({ children }) =>
      createElement(QueryClientProvider, { client: queryClient }, children),
  });
}

/** Answers the delete with `204`, recording the id of each repository it was asked to delete. */
function answerDelete() {
  const ids: string[] = [];
  server.use(
    apiHandler("delete", "/v1/Repositories/{id}", ({ params }) => {
      ids.push(params.id);
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return ids;
}

describe("useDeleteRepository", () => {
  it("sends DELETE /v1/Repositories/{id} for the repository it is given", async () => {
    const ids = answerDelete();
    const { result } = renderDeleteRepository(seededQueryClient());

    await act(() => result.current.mutateAsync(deleted.id));

    expect(ids).toEqual([deleted.id]);
  });

  it("invalidates the lists and removes only the deleted repository's detail query", async () => {
    answerDelete();
    const queryClient = seededQueryClient();
    const { result } = renderDeleteRepository(queryClient);

    await act(() => result.current.mutateAsync(deleted.id));

    expect(queryClient.getQueryState(listKey)?.isInvalidated).toBe(true);
    expect(
      queryClient.getQueryCache().find({
        queryKey: repositoryKeys.detail(deleted.id),
      }),
    ).toBeUndefined();
    expect(queryClient.getQueryData(repositoryKeys.detail(other.id))).toEqual(
      other,
    );
    expect(
      queryClient.getQueryState(repositoryKeys.detail(other.id))?.isInvalidated,
    ).toBe(false);
  });

  it("removes the detail query only once onDeleted resolves, staying pending until then", async () => {
    answerDelete();
    const queryClient = seededQueryClient();
    const step = deferred();
    const onDeleted = vi.fn(() => step.promise);
    const { result } = renderDeleteRepository(queryClient, { onDeleted });

    act(() => result.current.mutate(deleted.id));

    await waitFor(() => expect(onDeleted).toHaveBeenCalledOnce());
    expect(queryClient.getQueryState(listKey)?.isInvalidated).toBe(true);
    expect(queryClient.getQueryData(repositoryKeys.detail(deleted.id))).toEqual(
      deleted,
    );
    expect(result.current.isPending).toBe(true);

    await act(async () => step.resolve());

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(
      queryClient.getQueryCache().find({
        queryKey: repositoryKeys.detail(deleted.id),
      }),
    ).toBeUndefined();
  });

  it("stays pending until onDeleted rejects, then fails without removing the detail query", async () => {
    answerDelete();
    const queryClient = seededQueryClient();
    const step = deferred();
    const onDeleted = vi.fn(() => step.promise);
    const { result } = renderDeleteRepository(queryClient, { onDeleted });

    act(() => result.current.mutate(deleted.id));

    await waitFor(() => expect(onDeleted).toHaveBeenCalledOnce());
    expect(result.current.isPending).toBe(true);
    const failure = new Error("navigation failed");

    await act(async () => step.reject(failure));

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBe(failure);
    expect(queryClient.getQueryState(listKey)?.isInvalidated).toBe(true);
    expect(queryClient.getQueryData(repositoryKeys.detail(deleted.id))).toEqual(
      deleted,
    );
  });

  it("changes nothing in the cache and skips onDeleted when the delete fails", async () => {
    server.use(
      apiHandler("delete", "/v1/Repositories/{id}", () =>
        HttpResponse.json({ title: "Forbidden", status: 403 }, { status: 403 }),
      ),
    );
    const queryClient = seededQueryClient();
    const onDeleted = vi.fn(() => Promise.resolve());
    const { result } = renderDeleteRepository(queryClient, { onDeleted });

    act(() => result.current.mutate(deleted.id));

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ApiError);
    expect(result.current.error).toMatchObject({ status: 403 });
    expect(onDeleted).not.toHaveBeenCalled();
    expect(queryClient.getQueryState(listKey)?.isInvalidated).toBe(false);
    expect(queryClient.getQueryData(repositoryKeys.detail(deleted.id))).toEqual(
      deleted,
    );
  });
});
