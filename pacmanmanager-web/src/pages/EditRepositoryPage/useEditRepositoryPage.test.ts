import { QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import { createElement, Suspense } from "react";
import { describe, expect, it } from "vitest";
import type { Repository } from "../../api";
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

async function renderPageHook() {
  const queryClient = createQueryClient();
  const rendered = renderHook(() => useEditRepositoryPage(repositoryId), {
    wrapper: ({ children }) =>
      createElement(
        QueryClientProvider,
        { client: queryClient },
        createElement(Suspense, { fallback: null }, children),
      ),
  });
  await waitFor(() => expect(rendered.result.current).not.toBeNull());
  return rendered.result.current;
}

describe("useEditRepositoryPage", () => {
  it("returns the repository and the architectures, and that the caller may update it", async () => {
    const repository = aRepository({ id: repositoryId, name: "core-extras" });
    answerWith(repository);

    const page = await renderPageHook();

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

    const page = await renderPageHook();

    expect(page.canUpdate).toBe(false);
  });
});
