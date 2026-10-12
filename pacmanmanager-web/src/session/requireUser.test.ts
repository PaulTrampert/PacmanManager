import { isRedirect } from "@tanstack/react-router";
import { describe, expect, it } from "vitest";
import { currentUser } from "../testing/fixtures";
import { requireUser } from "./requireUser";

describe("requireUser", () => {
  it("puts a signed-in user on the route's context", () => {
    const user = currentUser();

    expect(
      requireUser({
        context: { session: { status: "signedIn", user } },
        location: { href: "/repositories/new" },
      }),
    ).toEqual({ user });
  });

  it("sends an anonymous visitor to /login by a full page navigation, returning them to the requested path and query", () => {
    let thrown: unknown;
    try {
      requireUser({
        context: { session: { status: "anonymous" } },
        location: { href: "/repositories?offset=20&sortBy=name" },
      });
    } catch (e) {
      thrown = e;
    }

    expect(isRedirect(thrown)).toBe(true);
    const { options } = thrown as {
      options: { href: string; reloadDocument: boolean };
    };
    expect(options.reloadDocument).toBe(true);
    const url = new URL(options.href, "http://web.test");
    expect(url.pathname).toBe("/login");
    expect(url.searchParams.get("redirectUri")).toBe(
      "/repositories?offset=20&sortBy=name",
    );
  });
});
