import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { ApiError } from "../api";
import { apiHandler } from "../mocks/api";
import { server } from "../mocks/node";
import { signedInHandlers } from "../mocks/session";
import { currentUser } from "../testing/fixtures";
import { userinfoPath } from "./paths";
import { fetchSession, SessionError } from "./session";

describe("fetchSession", () => {
  it("is anonymous when /userinfo answers 401, without asking the API", async () => {
    let apiCalled = false;
    server.use(
      apiHandler("get", "/v1/Users/me", () => {
        apiCalled = true;
        return HttpResponse.json(currentUser());
      }),
    );

    await expect(fetchSession()).resolves.toEqual({ status: "anonymous" });
    expect(apiCalled).toBe(false);
  });

  it("is signed in as the API's current user when /userinfo answers 200", async () => {
    const user = currentUser({ displayName: "Ada Lovelace" });
    server.use(...signedInHandlers(user));

    await expect(fetchSession()).resolves.toEqual({
      status: "signedIn",
      user,
    });
  });

  it("throws an ApiError when the API refuses the signed-in user", async () => {
    // The first handler to match a request answers it.
    server.use(
      apiHandler("get", "/v1/Users/me", () =>
        HttpResponse.json({ title: "Unauthorized" }, { status: 401 }),
      ),
      ...signedInHandlers(currentUser()),
    );

    const error = await fetchSession().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
  });

  it("throws a SessionError when /userinfo fails otherwise", async () => {
    server.use(
      http.get(userinfoPath, () => new HttpResponse(null, { status: 502 })),
    );

    const error = await fetchSession().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(SessionError);
    expect((error as SessionError).status).toBe(502);
  });
});
