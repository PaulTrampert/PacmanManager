import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { server } from "../mocks/node";
import { signedInHandlers } from "../mocks/session";
import { currentUser } from "../testing/fixtures";
import { renderRoute } from "../testing/renderRoute";

describe("the session in the app", () => {
  it("shows an anonymous visitor Sign in, returning them to the page they are on", async () => {
    renderRoute("/?tab=packages");

    const signIn = await screen.findByRole("link", { name: "Sign in" });
    expect(signIn).toHaveAttribute(
      "href",
      "/login?redirectUri=%2F%3Ftab%3Dpackages",
    );
    expect(
      screen.queryByRole("button", { name: "Sign out" }),
    ).not.toBeInTheDocument();
  });

  it("shows a signed-in user their display name and Sign out", async () => {
    server.use(
      ...signedInHandlers(currentUser({ displayName: "Ada Lovelace" })),
    );

    renderRoute("/");

    expect(await screen.findByText("Ada Lovelace")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Sign out" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Sign in" }),
    ).not.toBeInTheDocument();
  });

  it("resolves the session before rendering any route", async () => {
    server.use(
      ...signedInHandlers(currentUser({ displayName: "Ada Lovelace" })),
    );

    const { router } = renderRoute("/");

    await screen.findByRole("heading", { name: "Welcome to PacmanManager" });
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(router.state.matches[0].context.session).toMatchObject({
      status: "signedIn",
    });
  });
});
