import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderRoute } from "../testing/renderRoute";

describe("the repository list route's stub", () => {
  it("matches /repositories and renders its placeholder", async () => {
    const { router } = renderRoute("/repositories");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Repositories" }),
    ).toBeInTheDocument();
    expect(router.state.matches.at(-1)?.routeId).toBe("/repositories/");
  });
});
