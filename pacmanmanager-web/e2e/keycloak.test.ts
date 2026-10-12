import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../src/mocks/node";
import {
  adminToken,
  KeycloakAdminError,
  signOutSessions,
  userId,
} from "./keycloak.ts";

const tokenUrl =
  "http://localhost:8080/realms/master/protocol/openid-connect/token";
const usersUrl = "http://localhost:8080/admin/realms/localdev/users";

function issuesToken() {
  return http.post(tokenUrl, () =>
    HttpResponse.json({ access_token: "admin-token" }),
  );
}

describe("adminToken", () => {
  it("signs the bootstrap admin in to the master realm with a password grant", async () => {
    let form: URLSearchParams | undefined;
    server.use(
      http.post(tokenUrl, async ({ request }) => {
        form = new URLSearchParams(await request.text());
        return HttpResponse.json({ access_token: "admin-token" });
      }),
    );

    await expect(adminToken()).resolves.toBe("admin-token");
    expect(Object.fromEntries(form!)).toEqual({
      grant_type: "password",
      client_id: "admin-cli",
      username: "admin",
      password: "admin",
    });
  });

  it("throws when Keycloak refuses", async () => {
    server.use(
      http.post(tokenUrl, () => new HttpResponse("nope", { status: 401 })),
    );

    await expect(adminToken()).rejects.toThrow(KeycloakAdminError);
  });
});

describe("userId", () => {
  it("looks the user up by exact username with the admin token", async () => {
    let request: Request | undefined;
    server.use(
      issuesToken(),
      http.get(usersUrl, ({ request: r }) => {
        request = r;
        return HttpResponse.json([{ id: "user-id" }]);
      }),
    );

    await expect(userId("test")).resolves.toBe("user-id");
    const url = new URL(request!.url);
    expect(url.searchParams.get("username")).toBe("test");
    expect(url.searchParams.get("exact")).toBe("true");
    expect(request!.headers.get("Authorization")).toBe("Bearer admin-token");
  });

  it("throws when no user has the name", async () => {
    server.use(
      issuesToken(),
      http.get(usersUrl, () => HttpResponse.json([])),
    );

    await expect(userId("nobody")).rejects.toThrow(KeycloakAdminError);
  });
});

describe("signOutSessions", () => {
  it("posts to the user's logout endpoint", async () => {
    let loggedOut: string | undefined;
    server.use(
      issuesToken(),
      http.get(usersUrl, () => HttpResponse.json([{ id: "user-id" }])),
      http.post(`${usersUrl}/:id/logout`, ({ params, request }) => {
        expect(request.headers.get("Authorization")).toBe("Bearer admin-token");
        loggedOut = params.id as string;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    await signOutSessions("test");

    expect(loggedOut).toBe("user-id");
  });
});
