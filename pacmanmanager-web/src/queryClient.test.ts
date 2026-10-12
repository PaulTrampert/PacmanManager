import { HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { client, unwrap } from "./api";
import { apiHandler } from "./mocks/api";
import { server } from "./mocks/node";
import {
  createQueryClient,
  isClientError,
  maxQueryRetries,
  queryStaleTime,
  shouldRetryQuery,
} from "./queryClient";

class StatusError extends Error {
  constructor(readonly status: number) {
    super(`HTTP ${status}`);
  }
}

async function countAttempts(error: Error): Promise<number> {
  const queryClient = createQueryClient();
  const queryFn = vi.fn(() => Promise.reject(error));
  await expect(
    queryClient.fetchQuery({
      queryKey: ["attempts"],
      queryFn,
      // fetchQuery does not retry unless asked; take the default rule, without the back-off.
      retry: queryClient.getDefaultOptions().queries?.retry,
      retryDelay: 0,
    }),
  ).rejects.toBe(error);
  return queryFn.mock.calls.length;
}

describe("isClientError", () => {
  it.each([400, 401, 403, 404, 409, 499])("is true for status %i", (status) => {
    expect(isClientError(new StatusError(status))).toBe(true);
  });

  it.each([399, 500, 503])("is false for status %i", (status) => {
    expect(isClientError(new StatusError(status))).toBe(false);
  });

  it.each([
    ["an error without a status", new TypeError("Failed to fetch")],
    ["a non-numeric status", { status: "404" }],
    ["null", null],
    ["a string", "404"],
  ])("is false for %s", (_, error) => {
    expect(isClientError(error)).toBe(false);
  });
});

describe("shouldRetryQuery", () => {
  it("never retries a client error", () => {
    expect(shouldRetryQuery(0, new StatusError(404))).toBe(false);
  });

  it(`retries any other failure ${maxQueryRetries} times`, () => {
    const error = new StatusError(503);
    expect(shouldRetryQuery(0, error)).toBe(true);
    expect(shouldRetryQuery(maxQueryRetries - 1, error)).toBe(true);
    expect(shouldRetryQuery(maxQueryRetries, error)).toBe(false);
  });
});

describe("createQueryClient", () => {
  it("applies the app's query and mutation defaults", () => {
    const { queries, mutations } = createQueryClient().getDefaultOptions();

    expect(queries?.retry).toBe(shouldRetryQuery);
    expect(queries?.staleTime).toBe(queryStaleTime);
    expect(queries?.refetchOnWindowFocus).toBe(true);
    expect(mutations?.retry).toBe(false);
  });

  it("requests a query failing with a 4xx once", async () => {
    expect(await countAttempts(new StatusError(404))).toBe(1);
  });

  it("requests a query failing with a 5xx three times", async () => {
    expect(await countAttempts(new StatusError(503))).toBe(3);
  });
});

describe("a query through the API client", () => {
  async function countRequests(status: number): Promise<number> {
    let requests = 0;
    server.use(
      apiHandler("get", "/v1/Repositories/{id}", () => {
        requests++;
        return HttpResponse.json(
          { title: `Failed with ${status}.` },
          { status },
        );
      }),
    );
    const queryClient = createQueryClient();
    await expect(
      queryClient.fetchQuery({
        queryKey: ["repositories", "core"],
        queryFn: () =>
          unwrap(
            client.GET("/v1/Repositories/{id}", {
              params: { path: { id: "core" } },
            }),
          ),
        retryDelay: 0,
      }),
    ).rejects.toMatchObject({ status });
    return requests;
  }

  it("is requested once when it fails with 404", async () => {
    expect(await countRequests(404)).toBe(1);
  });

  it("is requested three times when it fails with 503", async () => {
    expect(await countRequests(503)).toBe(3);
  });
});
