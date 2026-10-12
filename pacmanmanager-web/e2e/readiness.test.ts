import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../src/mocks/node";
import { StackNotReadyError, waitUntilReady } from "./readiness.ts";

const web = "http://web.test/";
const auth = "http://auth.test/discovery";
const quick = { timeoutMs: 200, intervalMs: 10 };

describe("waitUntilReady", () => {
  it("resolves once every address answers", async () => {
    server.use(
      http.get(web, () => new HttpResponse("ok")),
      http.get(auth, () => HttpResponse.json({})),
    );

    await expect(waitUntilReady([web, auth], quick)).resolves.toBeUndefined();
  });

  it("keeps asking an address until it answers with a success status", async () => {
    let requests = 0;
    server.use(
      http.get(web, () => {
        requests += 1;
        return requests < 3
          ? new HttpResponse(null, { status: 503 })
          : new HttpResponse("ok");
      }),
    );

    await waitUntilReady([web], quick);

    expect(requests).toBe(3);
  });

  it("does not ask again an address that has answered", async () => {
    let webRequests = 0;
    let authRequests = 0;
    server.use(
      http.get(web, () => {
        webRequests += 1;
        return new HttpResponse("ok");
      }),
      http.get(auth, () => {
        authRequests += 1;
        return authRequests < 3 ? HttpResponse.error() : HttpResponse.json({});
      }),
    );

    await waitUntilReady([web, auth], quick);

    expect(webRequests).toBe(1);
  });

  it("names the compose command and the addresses still down when time runs out", async () => {
    server.use(
      http.get(web, () => new HttpResponse("ok")),
      http.get(auth, () => HttpResponse.error()),
    );

    const error = await waitUntilReady([web, auth], quick).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(StackNotReadyError);
    expect((error as StackNotReadyError).pending).toEqual([auth]);
    expect((error as Error).message).toContain("docker compose up -d --build");
    expect((error as Error).message).toContain(auth);
  });
});
