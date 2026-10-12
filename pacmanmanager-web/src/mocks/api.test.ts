import { describe, expect, it } from "vitest";
import { toMockPath } from "./api";

describe("toMockPath", () => {
  it("puts the path under the API's base URL", () => {
    expect(toMockPath("/v1/Users/me")).toBe("/api/pacmanmanager/v1/Users/me");
  });

  it("turns each path parameter into MSW's form", () => {
    expect(toMockPath("/v1/repositories/{repositoryId}/packages/{name}")).toBe(
      "/api/pacmanmanager/v1/repositories/:repositoryId/packages/:name",
    );
  });
});
