import { QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import type { ReactNode } from "react";
import { Suspense } from "react";
import { describe, expect, it } from "vitest";
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

describe("useRepositoryPage", () => {
  it("reads the repository by id and the architectures", async () => {
    server.use(
      apiHandler("get", "/v1/Repositories/{id}", ({ params }) =>
        params.id === repository.id
          ? HttpResponse.json(repository)
          : HttpResponse.json({ status: 404 }, { status: 404 }),
      ),
      apiHandler("get", "/v1/Architectures", () =>
        HttpResponse.json(architectures),
      ),
    );

    const { result } = renderHook(() => useRepositoryPage(repository.id), {
      wrapper,
    });

    await waitFor(() => expect(result.current).not.toBeNull());
    expect(result.current.repository).toEqual(repository);
    expect(result.current.architectures).toEqual(architectures);
  });
});
