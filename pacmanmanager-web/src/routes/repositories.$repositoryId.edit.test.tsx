import { linkOptions } from "@tanstack/react-router";
import { act, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderRoute } from "../testing/renderRoute";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

describe("the edit repository route's stub", () => {
  it("matches /repositories/$repositoryId/edit and renders its placeholder", async () => {
    const { router } = renderRoute(`/repositories/${repositoryId}/edit`);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Edit repository" }),
    ).toBeInTheDocument();
    const match = router.state.matches.at(-1);
    expect(match?.routeId).toBe("/repositories/$repositoryId/edit");
    expect(match?.params).toEqual({ repositoryId });
  });

  it("is reached by a typed link", async () => {
    // Starts on a page that fetches nothing, as `/` no longer is: it redirects to the list.
    const { router } = renderRoute("/no-such-page");
    const link = linkOptions({
      to: "/repositories/$repositoryId/edit",
      params: { repositoryId },
    });

    await act(() => router.navigate(link));

    expect(
      await screen.findByRole("heading", { level: 1, name: "Edit repository" }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(
      `/repositories/${repositoryId}/edit`,
    );
  });
});
