import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../src/mocks/node";
import {
  adminToken,
  KeycloakAdminError,
  setAccessTokenLifespan,
  signOutSessions,
  userId,
  webHostClientId,
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
  it("signs the user's sessions out, then revokes the web host's offline tokens", async () => {
    const calls: string[] = [];
    server.use(
      issuesToken(),
      http.get(usersUrl, () => HttpResponse.json([{ id: "user-id" }])),
      http.post(`${usersUrl}/:id/logout`, ({ params, request }) => {
        expect(request.headers.get("Authorization")).toBe("Bearer admin-token");
        calls.push(`logout ${params.id as string}`);
        return new HttpResponse(null, { status: 204 });
      }),
      http.delete(`${usersUrl}/:id/consents/:client`, ({ params, request }) => {
        expect(request.headers.get("Authorization")).toBe("Bearer admin-token");
        calls.push(`revoke ${params.id as string} ${params.client as string}`);
        return new HttpResponse(null, { status: 204 });
      }),
    );

    await signOutSessions("test");

    expect(calls).toEqual([
      "logout user-id",
      `revoke user-id ${webHostClientId}`,
    ]);
  });

  it("succeeds when the user has no offline token to revoke", async () => {
    server.use(
      issuesToken(),
      http.get(usersUrl, () => HttpResponse.json([{ id: "user-id" }])),
      http.post(
        `${usersUrl}/:id/logout`,
        () => new HttpResponse(null, { status: 204 }),
      ),
      http.delete(
        `${usersUrl}/:id/consents/:client`,
        () => new HttpResponse("not found", { status: 404 }),
      ),
    );

    await expect(signOutSessions("test")).resolves.toBeUndefined();
  });

  it("throws when Keycloak refuses to revoke the offline tokens", async () => {
    server.use(
      issuesToken(),
      http.get(usersUrl, () => HttpResponse.json([{ id: "user-id" }])),
      http.post(
        `${usersUrl}/:id/logout`,
        () => new HttpResponse(null, { status: 204 }),
      ),
      http.delete(
        `${usersUrl}/:id/consents/:client`,
        () => new HttpResponse("nope", { status: 403 }),
      ),
    );

    await expect(signOutSessions("test")).rejects.toThrow(KeycloakAdminError);
  });
});

describe("setAccessTokenLifespan", () => {
  const clientsUrl = "http://localhost:8080/admin/realms/localdev/clients";

  function hasClient(attributes: Record<string, string>) {
    return http.get(clientsUrl, ({ request }) => {
      expect(new URL(request.url).searchParams.get("clientId")).toBe(
        webHostClientId,
      );
      return HttpResponse.json([
        { id: "client-uuid", clientId: webHostClientId, attributes },
      ]);
    });
  }

  function recordsUpdate(sent: unknown[]) {
    return http.put(`${clientsUrl}/client-uuid`, async ({ request }) => {
      expect(request.headers.get("Authorization")).toBe("Bearer admin-token");
      sent.push(await request.json());
      return new HttpResponse(null, { status: 204 });
    });
  }

  it("sends the client back with the lifespan set, keeping its other attributes", async () => {
    const sent: unknown[] = [];
    server.use(
      issuesToken(),
      hasClient({ other: "kept" }),
      recordsUpdate(sent),
    );

    await expect(
      setAccessTokenLifespan(webHostClientId, 10),
    ).resolves.toBeUndefined();
    expect(sent).toEqual([
      {
        id: "client-uuid",
        clientId: webHostClientId,
        attributes: { other: "kept", "access.token.lifespan": "10" },
      },
    ]);
  });

  it("removes the override and returns the one it replaced", async () => {
    const sent: unknown[] = [];
    server.use(
      issuesToken(),
      hasClient({ "access.token.lifespan": "10" }),
      recordsUpdate(sent),
    );

    await expect(
      setAccessTokenLifespan(webHostClientId, undefined),
    ).resolves.toBe(10);
    expect(sent).toEqual([
      {
        id: "client-uuid",
        clientId: webHostClientId,
        attributes: { "access.token.lifespan": null },
      },
    ]);
  });

  it("throws when no client has the id", async () => {
    server.use(
      issuesToken(),
      http.get(clientsUrl, () => HttpResponse.json([])),
    );

    await expect(setAccessTokenLifespan(webHostClientId, 10)).rejects.toThrow(
      KeycloakAdminError,
    );
  });
});
