import { linkOptions } from "@tanstack/react-router";
import { act, screen, within } from "@testing-library/react";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { Repository } from "../api";
import { forbiddenText } from "../components/ForbiddenMessage";
import { apiHandler } from "../mocks/api";
import { aRepository, anArchitecture, aUser } from "../mocks/fixtures";
import { server } from "../mocks/node";
import { signedInHandlers } from "../mocks/session";
import { renderRoute } from "../testing/renderRoute";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

const me = aUser({
  id: "11111111-1111-4111-8111-111111111111",
  displayName: "Paul Trampert",
});

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
  anArchitecture({ value: "aarch64", displayValue: "64-bit ARM (aarch64)" }),
];

/** The repository the page edits, as a caller who may update it sees it, with any field overridden. */
function theRepository(overrides: Partial<Repository> = {}): Repository {
  return aRepository({
    id: repositoryId,
    name: "core-extras",
    isPublic: true,
    supportedArchitectures: ["x86_64"],
    owner: { id: me.id, displayName: me.displayName },
    ...overrides,
  });
}

/**
 * Signs in as `me` and answers the page's requests: the repository, `404` for any other id, and the
 * architectures.
 */
function answerWith(repository: Repository) {
  server.use(
    ...signedInHandlers(me),
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

/**
 * How long to wait for the page to first render. Its code is split from the route tree and loaded
 * on first render, which can take over a second while other test files run.
 */
const firstRenderTimeout = 5_000;

/** Waits for the page, and asserts its heading and its breadcrumbs. */
async function expectEditPageHeader() {
  expect(
    await screen.findByRole(
      "heading",
      { level: 1, name: "Edit repository" },
      { timeout: firstRenderTimeout },
    ),
  ).toBeInTheDocument();
  const trail = screen.getByRole("navigation", { name: "breadcrumb" });
  expect(
    within(trail).getByRole("link", { name: "Repositories" }),
  ).toHaveAttribute("href", "/repositories");
  expect(
    within(trail).getByRole("link", { name: "core-extras" }),
  ).toHaveAttribute("href", `/repositories/${repositoryId}`);
  expect(within(trail).getByText("Edit")).toHaveAttribute(
    "aria-current",
    "page",
  );
}

describe("the edit repository route", () => {
  it("shows the form filled in with the repository, with Save disabled until something changes", async () => {
    answerWith(theRepository());
    const { router } = renderRoute(`/repositories/${repositoryId}/edit`);

    await expectEditPageHeader();
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue(
      "core-extras",
    );
    expect(screen.getByRole("radio", { name: "Public" })).toBeChecked();
    expect(screen.getByRole("radio", { name: "Private" })).not.toBeChecked();
    const architecturesField = screen.getByRole("combobox", {
      name: "Architectures",
    });
    expect(architecturesField).toHaveAttribute("aria-disabled", "true");
    expect(architecturesField).toHaveTextContent("64-bit Intel/AMD (x86_64)");
    expect(architecturesField).not.toHaveTextContent("64-bit ARM (aarch64)");
    expect(screen.getByRole("button", { name: "Save" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Cancel" })).toBeEnabled();
    expect(screen.queryByText(forbiddenText)).not.toBeInTheDocument();
    const match = router.state.matches.at(-1);
    expect(match?.routeId).toBe("/repositories/$repositoryId/edit");
    expect(match?.params).toEqual({ repositoryId });
  });

  it("shows the 403 message in place of the form when the caller may not update it", async () => {
    answerWith(
      theRepository({ permissions: { update: false, delete: false } }),
    );
    renderRoute(`/repositories/${repositoryId}/edit`);

    await expectEditPageHeader();
    expect(screen.getByText(forbiddenText)).toBeVisible();
    expect(
      screen.queryByRole("textbox", { name: "Name" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Save" }),
    ).not.toBeInTheDocument();
  });

  it("is the not-found page when the API answers 404", async () => {
    answerWith(theRepository());
    renderRoute("/repositories/00000000-0000-4000-8000-000000000000/edit");

    expect(
      await screen.findByRole(
        "heading",
        { level: 1, name: "Page not found" },
        { timeout: firstRenderTimeout },
      ),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { level: 1, name: "Edit repository" }),
    ).not.toBeInTheDocument();
  });

  it("is reached by a typed link", async () => {
    answerWith(theRepository());
    // Starts on a page that fetches nothing, as `/` does not: it redirects to the list.
    const { router } = renderRoute("/no-such-page");
    const link = linkOptions({
      to: "/repositories/$repositoryId/edit",
      params: { repositoryId },
    });

    await act(() => router.navigate(link));

    await expectEditPageHeader();
    expect(router.state.location.pathname).toBe(
      `/repositories/${repositoryId}/edit`,
    );
  });
});
