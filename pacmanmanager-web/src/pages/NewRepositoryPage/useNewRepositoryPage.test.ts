import { QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import { createElement, Suspense } from "react";
import { describe, expect, it } from "vitest";
import { apiHandler } from "../../mocks/api";
import { anArchitecture } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { createQueryClient } from "../../queryClient";
import { useNewRepositoryPage } from "./useNewRepositoryPage";

describe("useNewRepositoryPage", () => {
  it("gives the page the architectures from GET /v1/Architectures", async () => {
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
    server.use(
      apiHandler("get", "/v1/Architectures", () =>
        HttpResponse.json(architectures),
      ),
    );
    const queryClient = createQueryClient();

    const { result } = renderHook(() => useNewRepositoryPage(), {
      wrapper: ({ children }) =>
        createElement(
          QueryClientProvider,
          { client: queryClient },
          createElement(Suspense, null, children),
        ),
    });

    await waitFor(() => expect(result.current).not.toBeNull());
    expect(result.current.architectures).toEqual(architectures);
  });
});
