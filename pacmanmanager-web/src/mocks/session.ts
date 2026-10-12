import { http, HttpResponse, type HttpHandler } from "msw";
import type { CurrentUser } from "../api";
import { userinfoPath } from "../session/paths";
import { apiHandler } from "./api";

/** Answers `GET /userinfo` as the web host does for a visitor who has not signed in: `401`. */
export function anonymousUserinfoHandler(): HttpHandler {
  return http.get(userinfoPath, () => new HttpResponse(null, { status: 401 }));
}

/**
 * Answers the session's requests as the web host does for `user` once signed in: `GET /userinfo`
 * with their claims, and `GET /v1/Users/me` with their record.
 */
export function signedInHandlers(user: CurrentUser): HttpHandler[] {
  return [
    http.get(userinfoPath, () =>
      HttpResponse.json({ sub: user.id, name: user.displayName }),
    ),
    apiHandler("get", "/v1/Users/me", () => HttpResponse.json(user)),
  ];
}
