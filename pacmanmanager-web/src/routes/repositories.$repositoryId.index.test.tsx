import { linkOptions } from "@tanstack/react-router";
import { act, screen, within } from "@testing-library/react";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiHandler } from "../mocks/api";
import { aRepository, anArchitecture } from "../mocks/fixtures";
import { server } from "../mocks/node";
import { renderRoute } from "../testing/renderRoute";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

/** Answers the page's requests: `repositoryId` is `core-extras`, and any other id is a `404`. */
function answerRequests() {
  server.use(
    apiHandler("get", "/v1/Repositories/{id}", ({ params }) =>
      params.id === repositoryId
        ? HttpResponse.json(
            aRepository({ id: repositoryId, name: "core-extras" }),
          )
        : HttpResponse.json(
            { title: "Not Found", status: 404 },
            { status: 404 },
          ),
    ),
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json([anArchitecture()]),
    ),
  );
}

describe("the repository route", () => {
  it("is the not-found page when the API answers 404", async () => {
    answerRequests();
    renderRoute("/repositories/00000000-0000-4000-8000-000000000000");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Page not found" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { level: 1, name: "core-extras" }),
    ).not.toBeInTheDocument();
  });

  it("is reached by a typed link, and gives the page its links", async () => {
    answerRequests();
    // Starts on a page that fetches nothing, as `/` no longer is: it redirects to the list.
    const { router } = renderRoute("/no-such-page");
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
    const match = router.state.matches.at(-1);
    expect(match?.routeId).toBe("/repositories/$repositoryId/");
    expect(match?.params).toEqual({ repositoryId });
    const trail = screen.getByRole("navigation", { name: "breadcrumb" });
    expect(
      within(trail).getByRole("link", { name: "Repositories" }),
    ).toHaveAttribute("href", "/repositories");
    expect(screen.getByRole("link", { name: "Edit" })).toHaveAttribute(
      "href",
      `/repositories/${repositoryId}/edit`,
    );
  });
});
