import { describe, expect, it } from "vitest";
import { isWebHostPath } from "./webHostPaths";

describe("isWebHostPath", () => {
  it.each([
    "/api",
    "/api/pacmanmanager/v1/repositories",
    "/login",
    "/logout",
    "/userinfo",
    "/antiforgery",
    "/signin-oidc",
    "/signout-callback-oidc",
  ])("is true for %s", (pathname) => {
    expect(isWebHostPath(pathname)).toBe(true);
  });

  it.each(["/", "/repositories", "/apiary", "/src/main.tsx"])(
    "is false for %s",
    (pathname) => {
      expect(isWebHostPath(pathname)).toBe(false);
    },
  );
});
