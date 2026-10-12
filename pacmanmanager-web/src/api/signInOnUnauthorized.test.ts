import { HttpResponse } from "msw";
import createClient from "openapi-fetch";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { antiforgeryHandler, apiHandler } from "../mocks/api";
import { server } from "../mocks/node";
import { currentUser } from "../testing/fixtures";
import { antiforgeryMiddleware } from "./antiforgery";
import { apiBaseUrl } from "./apiPaths";
import type { paths } from "./schema.gen";
import { signInOnUnauthorizedMiddleware } from "./signInOnUnauthorized";

const repositoryId = "6f1c1f0e-8a52-4d43-9d0b-2f1a3c5e7d90";

const navigate = vi.fn<(url: string) => void>();

// A client like the app's, but leaving the app through `navigate`, which jsdom cannot do.
const client = createClient<paths>({
  baseUrl: apiBaseUrl,
  credentials: "same-origin",
  fetch: (request) => globalThis.fetch(request),
});
client.use(antiforgeryMiddleware, signInOnUnauthorizedMiddleware(navigate));

/** Whether `call` is still pending once everything already queued has run. */
async function isPending(call: Promise<unknown>): Promise<boolean> {
  const pending = Symbol("pending");
  const outcome = await Promise.race([
    call.then(
      () => "settled",
      () => "settled",
    ),
    new Promise((resolve) => setTimeout(resolve, 20, pending)),
  ]);
  return outcome === pending;
}

let previousUrl: string;

beforeEach(() => {
  previousUrl = window.location.href;
  window.history.pushState(null, "", "/repositories?offset=20&sortBy=name");
});

afterEach(() => {
  window.history.pushState(null, "", previousUrl);
  navigate.mockReset();
});

describe("a 401 from the API", () => {
  it("sends the browser to sign in, returning it to the current path and query", async () => {
    server.use(
      apiHandler("get", "/v1/Users/me", () =>
        HttpResponse.json({ title: "Unauthorized" }, { status: 401 }),
      ),
    );

    void client.GET("/v1/Users/me");

    await vi.waitFor(() => expect(navigate).toHaveBeenCalledOnce());
    const url = new URL(navigate.mock.calls[0][0], "http://web.test");
    expect(url.pathname).toBe("/login");
    expect(url.searchParams.get("redirectUri")).toBe(
      "/repositories?offset=20&sortBy=name",
    );
  });

  it("leaves the call pending, so nothing renders an error while the browser leaves", async () => {
    server.use(
      apiHandler("get", "/v1/Users/me", () =>
        HttpResponse.json({ title: "Unauthorized" }, { status: 401 }),
      ),
    );

    const call = client.GET("/v1/Users/me");

    await vi.waitFor(() => expect(navigate).toHaveBeenCalled());
    expect(await isPending(call)).toBe(true);
  });

  it("sends the browser to sign in from a mutation too", async () => {
    server.use(
      antiforgeryHandler("token"),
      apiHandler("delete", "/v1/Repositories/{id}", () =>
        HttpResponse.json({ title: "Unauthorized" }, { status: 401 }),
      ),
    );

    void client.DELETE("/v1/Repositories/{id}", {
      params: { path: { id: repositoryId } },
    });

    await vi.waitFor(() => expect(navigate).toHaveBeenCalledOnce());
  });
});

describe("any other response", () => {
  it("is returned without leaving the app", async () => {
    const user = currentUser();
    server.use(
      apiHandler("get", "/v1/Users/me", () => HttpResponse.json(user)),
    );

    const { data } = await client.GET("/v1/Users/me");

    expect(data).toEqual(user);
    expect(navigate).not.toHaveBeenCalled();
  });

  it.each([400, 403, 404, 500])(
    "with status %i is returned without leaving the app",
    async (status) => {
      server.use(
        apiHandler("get", "/v1/Repositories/{id}", () =>
          HttpResponse.json({ title: "Failed" }, { status }),
        ),
      );

      const { response } = await client.GET("/v1/Repositories/{id}", {
        params: { path: { id: repositoryId } },
      });

      expect(response.status).toBe(status);
      expect(navigate).not.toHaveBeenCalled();
    },
  );
});
