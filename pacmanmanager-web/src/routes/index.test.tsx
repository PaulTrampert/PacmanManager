import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderRoute } from "../testing/renderRoute";

describe("the index route", () => {
  it("renders its heading inside the app shell", async () => {
    renderRoute("/");

    expect(
      await screen.findByRole("heading", {
        level: 1,
        name: "Welcome to PacmanManager",
      }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "PacmanManager" })).toHaveAttribute(
      "href",
      "/",
    );
  });
});
