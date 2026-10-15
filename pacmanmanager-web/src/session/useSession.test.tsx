import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { server } from "../mocks/node";
import { signedInHandlers } from "../mocks/session";
import { aUser } from "../mocks/fixtures";
import { renderRoute } from "../testing/renderRoute";

// Each test renders an unknown path, whose page fetches nothing but the session.
describe("the session in the app", () => {
  it("shows an anonymous visitor Sign in, returning them to the page they are on", async () => {
    renderRoute("/no-such-page?tab=packages");

    const signIn = await screen.findByRole("link", { name: "Sign in" });
    expect(signIn).toHaveAttribute(
      "href",
      "/login?redirectUri=%2Fno-such-page%3Ftab%3Dpackages",
    );
    expect(
      screen.queryByRole("button", { name: "Sign out" }),
    ).not.toBeInTheDocument();
  });

  it("shows a signed-in user their display name, with Sign out in its menu", async () => {
    server.use(...signedInHandlers(aUser({ displayName: "Ada Lovelace" })));

    renderRoute("/no-such-page");

    const trigger = await screen.findByRole("button", {
      name: "Ada Lovelace",
    });
    expect(
      screen.queryByRole("menuitem", { name: "Sign out" }),
    ).not.toBeInTheDocument();
    await userEvent.click(trigger);
    expect(
      await screen.findByRole("menuitem", { name: "Sign out" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Sign in" }),
    ).not.toBeInTheDocument();
  });

  it("resolves the session before rendering any route", async () => {
    server.use(...signedInHandlers(aUser({ displayName: "Ada Lovelace" })));

    const { router } = renderRoute("/no-such-page");

    await screen.findByRole("heading", { name: "Page not found" });
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(router.state.matches[0].context.session).toMatchObject({
      status: "signedIn",
    });
  });
});
