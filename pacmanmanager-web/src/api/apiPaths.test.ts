import { describe, expect, it } from "vitest";
import { toClientPath, toClientPaths } from "./apiPaths";

describe("toClientPath", () => {
  it.each([
    ["/api/v1/Repositories", "/v1/Repositories"],
    ["/api/v1/Repositories/{id}", "/v1/Repositories/{id}"],
  ])(
    "maps the document's %s to the client's %s",
    (documentPath, clientPath) => {
      expect(toClientPath(documentPath)).toBe(clientPath);
    },
  );

  it.each(["/v1/Repositories", "/apiv1/Repositories", "/healthz"])(
    "rejects %s, which the web host does not proxy",
    (documentPath) => {
      expect(() => toClientPath(documentPath)).toThrow(RangeError);
    },
  );
});

describe("toClientPaths", () => {
  it("renames every path and keeps its item", () => {
    const item = { get: {} };

    expect(toClientPaths({ "/api/v1/Users/me": item })).toEqual({
      "/v1/Users/me": item,
    });
  });
});
