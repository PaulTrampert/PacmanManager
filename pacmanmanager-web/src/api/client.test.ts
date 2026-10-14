import { http, HttpResponse } from "msw";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { antiforgeryHandler, apiHandler } from "../mocks/api";
import { aRepository, aUser } from "../mocks/fixtures";
import { server } from "../mocks/node";
import {
  antiforgeryCookie,
  AntiforgeryError,
  antiforgeryHeader,
  antiforgeryPath,
} from "./antiforgery";
import { ApiError } from "./ApiError";
import { client } from "./client";
import { unwrap } from "./unwrap";

const repositoryId = "6f1c1f0e-8a52-4d43-9d0b-2f1a3c5e7d90";

const repository = aRepository({
  id: repositoryId,
  name: "custom",
  isPublic: false,
});

const currentUser = aUser();

interface SentRequest {
  method: string;
  path: string;
  token: string | null;
}

let sent: SentRequest[];

function recordRequest({ request }: { request: Request }) {
  sent.push({
    method: request.method,
    path: new URL(request.url).pathname,
    token: request.headers.get(antiforgeryHeader),
  });
}

function clearAntiforgeryCookie() {
  document.cookie = `${antiforgeryCookie}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/`;
}

beforeEach(() => {
  sent = [];
  server.events.on("request:start", recordRequest);
});

afterEach(() => {
  server.events.removeListener("request:start", recordRequest);
  clearAntiforgeryCookie();
});

describe("the antiforgery middleware", () => {
  const mutations = [
    {
      method: "POST",
      path: "/api/pacmanmanager/v1/Repositories",
      handler: apiHandler("post", "/v1/Repositories", () =>
        HttpResponse.json(repository, { status: 201 }),
      ),
      send: () =>
        client.POST("/v1/Repositories", {
          body: { name: "custom", supportedArchitectures: ["x86_64"] },
        }),
    },
    {
      method: "PUT",
      path: `/api/pacmanmanager/v1/Repositories/${repositoryId}`,
      handler: apiHandler("put", "/v1/Repositories/{id}", () =>
        HttpResponse.json(repository),
      ),
      send: () =>
        client.PUT("/v1/Repositories/{id}", {
          params: { path: { id: repositoryId } },
          body: { name: "custom", supportedArchitectures: ["x86_64"] },
        }),
    },
    {
      method: "PATCH",
      path: "/api/pacmanmanager/v1/Users/me",
      handler: apiHandler("patch", "/v1/Users/me", () =>
        HttpResponse.json(currentUser),
      ),
      send: () =>
        client.PATCH("/v1/Users/me", { body: { displayName: "Renamed" } }),
    },
    {
      method: "DELETE",
      path: `/api/pacmanmanager/v1/Repositories/${repositoryId}`,
      handler: apiHandler(
        "delete",
        "/v1/Repositories/{id}",
        () => new HttpResponse(null, { status: 204 }),
      ),
      send: () =>
        client.DELETE("/v1/Repositories/{id}", {
          params: { path: { id: repositoryId } },
        }),
    },
  ];

  it.each(mutations)(
    "fetches /antiforgery before a $method and sends the cookie's value",
    async ({ method, path, handler, send }) => {
      const token = `token-for-${method.toLowerCase()}`;
      server.use(antiforgeryHandler(token), handler);

      const { response } = await send();

      expect(response.ok).toBe(true);
      expect(sent).toEqual([
        { method: "GET", path: antiforgeryPath, token: null },
        { method, path, token },
      ]);
    },
  );

  it("fetches a fresh token for each mutation", async () => {
    server.use(
      apiHandler(
        "delete",
        "/v1/Repositories/{id}",
        () => new HttpResponse(null, { status: 204 }),
      ),
    );
    const remove = () =>
      client.DELETE("/v1/Repositories/{id}", {
        params: { path: { id: repositoryId } },
      });

    server.use(antiforgeryHandler("first"));
    await remove();
    server.use(antiforgeryHandler("second"));
    await remove();

    expect(sent.map(({ method, token }) => [method, token])).toEqual([
      ["GET", null],
      ["DELETE", "first"],
      ["GET", null],
      ["DELETE", "second"],
    ]);
  });

  it("neither fetches /antiforgery nor sends a token with a GET", async () => {
    // A token left over from an earlier mutation must not be sent either.
    document.cookie = `${antiforgeryCookie}=left-over; Path=/`;
    server.use(
      apiHandler("get", "/v1/Users/me", () => HttpResponse.json(currentUser)),
    );

    const user = await unwrap(client.GET("/v1/Users/me"));

    expect(user).toEqual(currentUser);
    expect(sent).toEqual([
      { method: "GET", path: "/api/pacmanmanager/v1/Users/me", token: null },
    ]);
  });

  it("does not send a mutation when /antiforgery fails", async () => {
    server.use(
      http.get(antiforgeryPath, () => new HttpResponse(null, { status: 500 })),
    );

    await expect(
      client.DELETE("/v1/Repositories/{id}", {
        params: { path: { id: repositoryId } },
      }),
    ).rejects.toBeInstanceOf(AntiforgeryError);
    expect(sent.map(({ method }) => method)).toEqual(["GET"]);
  });

  it("does not send a mutation when /antiforgery sets no cookie", async () => {
    server.use(http.get(antiforgeryPath, () => new HttpResponse(null)));

    await expect(
      client.DELETE("/v1/Repositories/{id}", {
        params: { path: { id: repositoryId } },
      }),
    ).rejects.toBeInstanceOf(AntiforgeryError);
    expect(sent.map(({ method }) => method)).toEqual(["GET"]);
  });
});

describe("an error response", () => {
  it("becomes an ApiError with its status and title", async () => {
    server.use(
      apiHandler("get", "/v1/Repositories/{id}", () =>
        HttpResponse.json(
          { status: 404, title: "Not Found", detail: "No such repository." },
          { status: 404 },
        ),
      ),
    );

    const error = await unwrap(
      client.GET("/v1/Repositories/{id}", {
        params: { path: { id: repositoryId } },
      }),
    ).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 404,
      title: "Not Found",
      detail: "No such repository.",
      errors: undefined,
    });
  });

  it("carries a validation response's errors", async () => {
    const errors = {
      Name: ["The Name field is required."],
      SupportedArchitectures: ["At least one architecture is required."],
    };
    server.use(
      apiHandler("post", "/v1/Repositories", () =>
        HttpResponse.json(
          {
            status: 400,
            title: "One or more validation errors occurred.",
            errors,
          },
          { status: 400 },
        ),
      ),
    );

    const error = await unwrap(
      client.POST("/v1/Repositories", {
        body: { name: "", supportedArchitectures: [] },
      }),
    ).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 400,
      title: "One or more validation errors occurred.",
      errors,
    });
  });
});
