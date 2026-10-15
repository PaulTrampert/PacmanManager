import { QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import { createElement, Suspense } from "react";
import { describe, expect, it, vi } from "vitest";
import { ApiError, type Repository } from "../../api";
import { apiHandler } from "../../mocks/api";
import { anArchitecture, aRepository } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { createQueryClient } from "../../queryClient";
import { useNewRepositoryPage } from "./useNewRepositoryPage";

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
  anArchitecture({
    value: "aarch64",
    displayValue: "64-bit ARM (aarch64)",
  }),
];

const created = aRepository({
  id: "22222222-2222-4222-8222-222222222222",
  name: "games",
  isPublic: true,
  supportedArchitectures: ["x86_64", "aarch64"],
});

const values = {
  name: "games",
  isPublic: true,
  supportedArchitectures: ["x86_64", "aarch64"],
};

function renderNewRepositoryPage(onCreated: (repository: Repository) => void) {
  server.use(
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json(architectures),
    ),
  );
  const queryClient = createQueryClient();
  return renderHook(() => useNewRepositoryPage(onCreated), {
    wrapper: ({ children }) =>
      createElement(
        QueryClientProvider,
        { client: queryClient },
        createElement(Suspense, null, children),
      ),
  });
}

async function renderLoaded(onCreated: (repository: Repository) => void) {
  const rendered = renderNewRepositoryPage(onCreated);
  await waitFor(() => expect(rendered.result.current).not.toBeNull());
  return rendered;
}

describe("useNewRepositoryPage", () => {
  it("gives the page the architectures from GET /v1/Architectures", async () => {
    const { result } = await renderLoaded(vi.fn());

    expect(result.current.architectures).toEqual(architectures);
    expect(result.current.pending).toBe(false);
    expect(result.current.error).toBeNull();
  });

  it("creates the repository with the form's values and hands it to onCreated", async () => {
    const bodies: unknown[] = [];
    server.use(
      apiHandler("post", "/v1/Repositories", async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(created, { status: 201 });
      }),
    );
    const onCreated = vi.fn();
    const { result } = await renderLoaded(onCreated);

    act(() => result.current.create(values));

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(created));
    expect(onCreated).toHaveBeenCalledOnce();
    expect(bodies).toEqual([values]);
  });

  it("is pending while the create is in flight", async () => {
    let answer!: () => void;
    server.use(
      apiHandler(
        "post",
        "/v1/Repositories",
        () =>
          new Promise((resolve) => {
            answer = () => resolve(HttpResponse.json(created, { status: 201 }));
          }),
      ),
    );
    const onCreated = vi.fn();
    const { result } = await renderLoaded(onCreated);

    act(() => result.current.create(values));

    await waitFor(() => expect(result.current.pending).toBe(true));
    answer();
    await waitFor(() => expect(result.current.pending).toBe(false));
    expect(onCreated).toHaveBeenCalledWith(created);
  });

  it("gives the page a failed create's error and does not call onCreated", async () => {
    server.use(
      apiHandler("post", "/v1/Repositories", () =>
        HttpResponse.json({ title: "Conflict", status: 409 }, { status: 409 }),
      ),
    );
    const onCreated = vi.fn();
    const { result } = await renderLoaded(onCreated);

    act(() => result.current.create(values));

    await waitFor(() => expect(result.current.error).toBeInstanceOf(ApiError));
    expect(result.current.error).toMatchObject({ status: 409 });
    expect(result.current.pending).toBe(false);
    expect(onCreated).not.toHaveBeenCalled();
  });
});
