import { describe, expect, it } from "vitest";
import { loginUrl } from "./loginUrl";

describe("loginUrl", () => {
  it("sends the user to /login with the path to return to", () => {
    expect(loginUrl("/")).toBe("/login?redirectUri=%2F");
  });

  it("keeps the query string of the path to return to", () => {
    const url = new URL(
      loginUrl("/repositories?offset=20&sortBy=name"),
      "http://web.test",
    );

    expect(url.pathname).toBe("/login");
    expect(url.searchParams.get("redirectUri")).toBe(
      "/repositories?offset=20&sortBy=name",
    );
  });
});
