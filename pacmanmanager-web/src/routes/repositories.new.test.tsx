import { screen, within } from "@testing-library/react";
import { HttpResponse } from "msw";
import { afterEach, describe, expect, it, vi } from "vitest";
import { apiHandler } from "../mocks/api";
import { anArchitecture, aUser } from "../mocks/fixtures";
import { server } from "../mocks/node";
import { signedInHandlers } from "../mocks/session";
import { renderRoute } from "../testing/renderRoute";

const x86_64 = anArchitecture({
  value: "x86_64",
  displayValue: "64-bit Intel/AMD (x86_64)",
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("the new repository route", () => {
  it("renders the new repository page for a signed-in user, its crumb linking to the list", async () => {
    server.use(
      ...signedInHandlers(aUser()),
      apiHandler("get", "/v1/Architectures", () => HttpResponse.json([x86_64])),
    );
    const { router } = renderRoute("/repositories/new");

    expect(
      await screen.findByRole("heading", { level: 1, name: "New repository" }),
    ).toBeInTheDocument();
    expect(router.state.matches.at(-1)?.routeId).toBe("/repositories/new");
    const breadcrumbs = screen.getByRole("navigation", { name: "breadcrumb" });
    expect(
      within(breadcrumbs).getByRole("link", { name: "Repositories" }),
    ).toHaveAttribute("href", "/repositories");
  });

  it("sends an anonymous visitor to sign in, returning them to the new repository page", async () => {
    const { router } = renderRoute("/repositories/new");
    // The redirect leaves the app by a full page navigation, which jsdom cannot perform.
    const navigate = vi.spyOn(router, "navigate").mockResolvedValue();

    await vi.waitFor(() => expect(navigate).toHaveBeenCalled());
    const [options] = navigate.mock.calls[0]!;
    expect(options).toMatchObject({ reloadDocument: true });
    const url = new URL(options.href!, "http://web.test");
    expect(url.pathname).toBe("/login");
    expect(url.searchParams.get("redirectUri")).toBe("/repositories/new");
    expect(
      screen.queryByRole("textbox", { name: "Name" }),
    ).not.toBeInTheDocument();
  });
});
