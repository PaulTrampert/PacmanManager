import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderRoute } from "../testing/renderRoute";

describe("the new repository route's stub", () => {
  it("matches /repositories/new and renders its placeholder", async () => {
    const { router } = renderRoute("/repositories/new");

    expect(
      await screen.findByRole("heading", { level: 1, name: "New repository" }),
    ).toBeInTheDocument();
    expect(router.state.matches.at(-1)?.routeId).toBe("/repositories/new");
  });
});
