import { QueryClient } from "@tanstack/react-query";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { ApiError } from "../../api";
import { apiHandler } from "../../mocks/api";
import { anArchitecture } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { queryStaleTime } from "../../queryClient";
import {
  architecturesQuery,
  architecturesStaleTime,
} from "./architecturesQuery";

describe("architecturesQuery", () => {
  it("is keyed by the resource name alone", () => {
    expect(architecturesQuery().queryKey).toEqual(["architectures"]);
  });

  it("sends GET /v1/Architectures with no parameters and returns its array", async () => {
    const architectures = [
      anArchitecture(),
      anArchitecture({
        value: "aarch64",
        displayValue: "64-bit ARM (aarch64)",
      }),
    ];
    const requests: URL[] = [];
    server.use(
      apiHandler("get", "/v1/Architectures", ({ request }) => {
        requests.push(new URL(request.url));
        return HttpResponse.json(architectures);
      }),
    );

    const data = await new QueryClient().fetchQuery(architecturesQuery());

    expect(data).toEqual(architectures);
    expect(requests).toHaveLength(1);
    expect(requests[0].search).toBe("");
  });

  it("stays fresh far longer than the default, since the list is static", () => {
    expect(architecturesQuery().staleTime).toBe(architecturesStaleTime);
    expect(architecturesStaleTime).toBeGreaterThan(queryStaleTime);
  });

  it("is not fetched again while it is fresh", async () => {
    let calls = 0;
    server.use(
      apiHandler("get", "/v1/Architectures", () => {
        calls += 1;
        return HttpResponse.json([anArchitecture()]);
      }),
    );
    const queryClient = new QueryClient();

    await queryClient.ensureQueryData(architecturesQuery());
    await queryClient.prefetchQuery(architecturesQuery());

    expect(calls).toBe(1);
  });

  it("throws an ApiError when the API fails", async () => {
    server.use(
      apiHandler(
        "get",
        "/v1/Architectures",
        () => new HttpResponse(null, { status: 500 }),
      ),
    );

    const error = await new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })
      .fetchQuery(architecturesQuery())
      .catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(500);
  });
});
