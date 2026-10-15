import { linkOptions } from "@tanstack/react-router";
import { act, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CurrentUser, Repository } from "../api";
import { apiHandler } from "../mocks/api";
import { aRepository, anArchitecture, aUser } from "../mocks/fixtures";
import { server } from "../mocks/node";
import { signedInHandlers } from "../mocks/session";
import { renderRoute } from "../testing/renderRoute";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

const owner = aUser({
  id: "11111111-1111-4111-8111-111111111111",
  displayName: "Paul Trampert",
});

const otherUser = aUser({
  id: "22222222-2222-4222-8222-222222222222",
  displayName: "Other User",
});

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
  anArchitecture({ value: "aarch64", displayValue: "64-bit ARM (aarch64)" }),
];

/** The repository the page shows, owned by `owner`, as a caller with `permissions` sees it. */
function theRepository(overrides: Partial<Repository> = {}): Repository {
  return aRepository({
    id: repositoryId,
    name: "core-extras",
    owner: { id: owner.id, displayName: owner.displayName },
    supportedArchitectures: ["x86_64", "aarch64"],
    serverUrl: "http://localhost:8082/pacman/core-extras/$arch",
    ...overrides,
  });
}

/** Answers the page's requests: the repository and the architectures, signed in as `user`. */
function answerWith(repository: Repository, user?: CurrentUser) {
  server.use(
    ...(user ? signedInHandlers(user) : []),
    apiHandler("get", "/v1/Repositories/{id}", ({ params }) =>
      params.id === repositoryId
        ? HttpResponse.json(repository)
        : HttpResponse.json(
            { title: "Not Found", status: 404 },
            { status: 404 },
          ),
    ),
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json(architectures),
    ),
  );
}

/** Waits for the page and asserts its title, breadcrumbs, details and collapsed section. */
async function expectRepositoryPage(visibility: "Public" | "Private") {
  expect(
    await screen.findByRole("heading", { level: 1, name: "core-extras" }),
  ).toBeInTheDocument();
  const trail = screen.getByRole("navigation", { name: "breadcrumb" });
  expect(
    within(trail).getByRole("link", { name: "Repositories" }),
  ).toHaveAttribute("href", "/repositories");
  expect(
    screen.getByRole("img", { name: `Visibility: ${visibility}` }),
  ).toBeInTheDocument();
  expect(detail("Owner")).toHaveTextContent("Paul Trampert");
  expect(detail("Architectures")).toHaveTextContent(
    "64-bit Intel/AMD (x86_64), 64-bit ARM (aarch64)",
  );
  expect(
    screen.getByRole("button", { name: /Use this repository/ }),
  ).toHaveAttribute("aria-expanded", "false");
}

/** The definition the details row gives for `term`. */
function detail(term: string) {
  return screen.getByText(term, { selector: "dt" }).nextElementSibling;
}

describe("the repository page", () => {
  it("shows its owner Edit and Delete, and the pacman.conf section once expanded", async () => {
    answerWith(theRepository(), owner);
    const { router } = renderRoute(`/repositories/${repositoryId}`);

    await expectRepositoryPage("Public");
    expect(screen.getByRole("link", { name: "Edit" })).toHaveAttribute(
      "href",
      `/repositories/${repositoryId}/edit`,
    );
    expect(screen.getByRole("button", { name: "Delete" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Paul Trampert" })).toBeVisible();
    const match = router.state.matches.at(-1);
    expect(match?.routeId).toBe("/repositories/$repositoryId/");
    expect(match?.params).toEqual({ repositoryId });

    await userEvent.click(
      screen.getByRole("button", { name: /Use this repository/ }),
    );

    expect(
      screen.getByText(/^\[core-extras\]/, { selector: "pre, code" }),
    ).toHaveTextContent(
      "[core-extras] SigLevel = Optional TrustAll Server = http://localhost:8082/pacman/core-extras/$arch",
    );
  });

  it("shows its owner a private repository with the private note", async () => {
    answerWith(theRepository({ isPublic: false }), owner);
    renderRoute(`/repositories/${repositoryId}`);

    await expectRepositoryPage("Private");
    await userEvent.click(
      screen.getByRole("button", { name: /Use this repository/ }),
    );

    expect(
      screen.getByText(/pacman must authenticate to read it/),
    ).toBeInTheDocument();
  });

  it("shows a signed-in user who does not own it neither Edit nor Delete", async () => {
    answerWith(
      theRepository({ permissions: { update: false, delete: false } }),
      otherUser,
    );
    renderRoute(`/repositories/${repositoryId}`);

    await expectRepositoryPage("Public");
    expect(screen.getByRole("button", { name: "Other User" })).toBeVisible();
    expect(
      screen.queryByRole("link", { name: "Edit" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Delete" }),
    ).not.toBeInTheDocument();
  });

  it("shows an anonymous visitor neither Edit nor Delete", async () => {
    answerWith(
      theRepository({ permissions: { update: false, delete: false } }),
    );
    renderRoute(`/repositories/${repositoryId}`);

    await expectRepositoryPage("Public");
    expect(screen.getByRole("link", { name: "Sign in" })).toBeVisible();
    expect(
      screen.queryByRole("link", { name: "Edit" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Delete" }),
    ).not.toBeInTheDocument();
  });

  it("is the not-found page when the API answers 404", async () => {
    answerWith(theRepository());
    renderRoute("/repositories/00000000-0000-4000-8000-000000000000");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Page not found" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { level: 1, name: "core-extras" }),
    ).not.toBeInTheDocument();
  });

  it("is reached by a typed link", async () => {
    answerWith(theRepository());
    const { router } = renderRoute("/");
    const link = linkOptions({
      to: "/repositories/$repositoryId",
      params: { repositoryId },
    });

    await act(() => router.navigate(link));

    expect(
      await screen.findByRole("heading", { level: 1, name: "core-extras" }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(
      `/repositories/${repositoryId}`,
    );
  });
});
