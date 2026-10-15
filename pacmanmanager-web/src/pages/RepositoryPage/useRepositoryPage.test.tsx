import { QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import type { ReactNode } from "react";
import { Suspense } from "react";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api";
import { apiHandler } from "../../mocks/api";
import { aRepository, anArchitecture } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { createQueryClient } from "../../queryClient";
import { useRepositoryPage } from "./useRepositoryPage";

const repository = aRepository({
  id: "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
  name: "core-extras",
});
const architectures = [anArchitecture()];

function wrapper({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={createQueryClient()}>
      <Suspense>{children}</Suspense>
    </QueryClientProvider>
  );
}

/** Answers the page's queries, and each `DELETE` with `deleteStatus`, counting the deletes. */
function answerRequests(deleteStatus = 204) {
  const deletes: string[] = [];
  server.use(
    apiHandler("get", "/v1/Repositories/{id}", ({ params }) =>
      params.id === repository.id
        ? HttpResponse.json(repository)
        : HttpResponse.json({ status: 404 }, { status: 404 }),
    ),
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json(architectures),
    ),
    apiHandler("delete", "/v1/Repositories/{id}", ({ params }) => {
      deletes.push(params.id);
      return deleteStatus === 204
        ? new HttpResponse(null, { status: 204 })
        : HttpResponse.json(
            { title: "Server error", status: deleteStatus },
            { status: deleteStatus },
          );
    }),
  );
  return { deletes };
}

async function renderPageHook(
  onDeleted: () => Promise<unknown> = () => Promise.resolve(),
) {
  const rendered = renderHook(
    () => useRepositoryPage(repository.id, onDeleted),
    { wrapper },
  );
  await waitFor(() => expect(rendered.result.current).not.toBeNull());
  return rendered;
}

describe("useRepositoryPage", () => {
  it("reads the repository by id and the architectures, with the dialog closed", async () => {
    answerRequests();

    const { result } = await renderPageHook();

    expect(result.current.repository).toEqual(repository);
    expect(result.current.architectures).toEqual(architectures);
    expect(result.current.deleteDialogOpen).toBe(false);
    expect(result.current.deletePending).toBe(false);
    expect(result.current.deleteError).toBeNull();
  });

  it("opens the delete dialog, and closes it on cancel without deleting", async () => {
    const { deletes } = answerRequests();
    const { result } = await renderPageHook();

    act(() => result.current.onDelete());
    expect(result.current.deleteDialogOpen).toBe(true);

    act(() => result.current.onDeleteCancel());
    expect(result.current.deleteDialogOpen).toBe(false);
    expect(deletes).toEqual([]);
  });

  it("deletes the repository on confirm and calls onDeleted, pending until it settles", async () => {
    const { deletes } = answerRequests();
    let finishNavigation: () => void = () => {};
    const onDeleted = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          finishNavigation = resolve;
        }),
    );
    const { result } = await renderPageHook(onDeleted);

    act(() => result.current.onDelete());
    act(() => result.current.onDeleteConfirm());

    await waitFor(() => expect(onDeleted).toHaveBeenCalledOnce());
    expect(deletes).toEqual([repository.id]);
    expect(result.current.deletePending).toBe(true);
    expect(result.current.deleteDialogOpen).toBe(true);

    await act(async () => finishNavigation());
    await waitFor(() => expect(result.current.deletePending).toBe(false));
    expect(result.current.deleteError).toBeNull();
  });

  it("closes the dialog on failure and keeps the error until it is dismissed", async () => {
    answerRequests(500);
    const onDeleted = vi.fn(() => Promise.resolve());
    const { result } = await renderPageHook(onDeleted);

    act(() => result.current.onDelete());
    act(() => result.current.onDeleteConfirm());

    await waitFor(() => expect(result.current.deleteDialogOpen).toBe(false));
    expect(result.current.deleteError).toBeInstanceOf(ApiError);
    expect(result.current.deletePending).toBe(false);
    expect(onDeleted).not.toHaveBeenCalled();

    act(() => result.current.onDeleteErrorDismiss());
    await waitFor(() => expect(result.current.deleteError).toBeNull());
  });
});
