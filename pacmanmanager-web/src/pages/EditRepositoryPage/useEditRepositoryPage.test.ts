import { QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import { createElement, Suspense } from "react";
import { describe, expect, it, vi } from "vitest";
import { ApiError, type Repository } from "../../api";
import { apiHandler } from "../../mocks/api";
import { aRepository, anArchitecture } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { createQueryClient } from "../../queryClient";
import { useEditRepositoryPage } from "./useEditRepositoryPage";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";
const architectures = [anArchitecture()];

function answerWith(repository: Repository) {
  server.use(
    apiHandler("get", "/v1/Repositories/{id}", () =>
      HttpResponse.json(repository),
    ),
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json(architectures),
    ),
  );
}

async function renderPageHook(onSaved: () => void = vi.fn()) {
  const queryClient = createQueryClient();
  const rendered = renderHook(
    () => useEditRepositoryPage({ repositoryId, onSaved }),
    {
      wrapper: ({ children }) =>
        createElement(
          QueryClientProvider,
          { client: queryClient },
          createElement(Suspense, { fallback: null }, children),
        ),
    },
  );
  await waitFor(() => expect(rendered.result.current).not.toBeNull());
  return rendered.result;
}

const stored = aRepository({
  id: repositoryId,
  name: "core-extras",
  isPublic: true,
  supportedArchitectures: ["x86_64"],
});
const edits = {
  name: "core-extras-renamed",
  isPublic: false,
  supportedArchitectures: ["x86_64"],
};

describe("useEditRepositoryPage", () => {
  it("returns the repository and the architectures, and that the caller may update it", async () => {
    const repository = aRepository({ id: repositoryId, name: "core-extras" });
    answerWith(repository);

    const page = (await renderPageHook()).current;

    expect(page.repository).toEqual(repository);
    expect(page.architectures).toEqual(architectures);
    expect(page.canUpdate).toBe(true);
  });

  it("says the caller may not update a repository whose permissions forbid it", async () => {
    answerWith(
      aRepository({
        id: repositoryId,
        permissions: { update: false, delete: false },
      }),
    );

    const page = (await renderPageHook()).current;

    expect(page.canUpdate).toBe(false);
  });

  it("opens the confirmation for the submitted values, sending nothing", async () => {
    answerWith(stored);
    const requests: unknown[] = [];
    server.use(
      apiHandler("put", "/v1/Repositories/{id}", async ({ request }) => {
        requests.push(await request.json());
        return HttpResponse.json(stored);
      }),
    );
    const result = await renderPageHook();
    expect(result.current.confirming).toBe(false);
    expect(result.current.edited).toBe(stored);

    act(() => result.current.onSubmit(edits));

    expect(result.current.confirming).toBe(true);
    expect(result.current.edited).toEqual(edits);
    expect(requests).toEqual([]);
  });

  it("closes the confirmation on cancel, keeping the edited values", async () => {
    answerWith(stored);
    const result = await renderPageHook();
    act(() => result.current.onSubmit(edits));

    act(() => result.current.onCancelConfirm());

    expect(result.current.confirming).toBe(false);
    expect(result.current.edited).toEqual(edits);
  });

  it("sends the edited values once confirmed, and calls onSaved when the update succeeds", async () => {
    answerWith(stored);
    const requests: { id: string; body: unknown }[] = [];
    server.use(
      apiHandler(
        "put",
        "/v1/Repositories/{id}",
        async ({ params, request }) => {
          requests.push({ id: params.id, body: await request.json() });
          return HttpResponse.json({ ...stored, ...edits });
        },
      ),
    );
    const onSaved = vi.fn();
    const result = await renderPageHook(onSaved);
    act(() => result.current.onSubmit(edits));

    act(() => result.current.onConfirm());

    await waitFor(() => expect(result.current.pending).toBe(true));
    expect(result.current.confirming).toBe(true);
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(requests).toEqual([{ id: repositoryId, body: edits }]);
    expect(result.current.error).toBeNull();
  });

  it("closes the confirmation and gives the form the error when the update fails", async () => {
    answerWith(stored);
    server.use(
      apiHandler("put", "/v1/Repositories/{id}", () =>
        HttpResponse.json({ title: "Conflict", status: 409 }, { status: 409 }),
      ),
    );
    const onSaved = vi.fn();
    const result = await renderPageHook(onSaved);
    act(() => result.current.onSubmit(edits));

    act(() => result.current.onConfirm());

    await waitFor(() => expect(result.current.confirming).toBe(false));
    expect(result.current.error).toBeInstanceOf(ApiError);
    expect((result.current.error as ApiError).status).toBe(409);
    expect(result.current.pending).toBe(false);
    expect(result.current.edited).toEqual(edits);
    expect(onSaved).not.toHaveBeenCalled();
  });
});
