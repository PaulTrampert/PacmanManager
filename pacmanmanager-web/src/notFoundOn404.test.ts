import { isNotFound } from "@tanstack/react-router";
import { describe, expect, it } from "vitest";
import { ApiError } from "./api";
import { notFoundOn404 } from "./notFoundOn404";

describe("notFoundOn404", () => {
  it("throws notFound() for an ApiError with status 404", () => {
    let thrown: unknown;
    try {
      notFoundOn404(new ApiError(404, { title: "Not Found" }));
    } catch (error) {
      thrown = error;
    }

    expect(isNotFound(thrown)).toBe(true);
  });

  it.each([
    ["an ApiError with status 403", new ApiError(403)],
    ["an ApiError with status 500", new ApiError(500)],
    ["a network failure", new TypeError("Failed to fetch")],
  ])("leaves %s alone", (_, error) => {
    expect(() => notFoundOn404(error)).not.toThrow();
  });
});
