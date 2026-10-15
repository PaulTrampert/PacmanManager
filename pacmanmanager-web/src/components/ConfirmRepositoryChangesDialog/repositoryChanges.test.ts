import { describe, expect, it } from "vitest";
import { aRepository } from "../../mocks/fixtures";
import { repositoryChanges } from "./repositoryChanges";

describe("repositoryChanges", () => {
  it("finds nothing when nothing changed", () => {
    const stored = aRepository({ name: "core-extras", isPublic: true });

    expect(repositoryChanges(stored, stored)).toEqual([]);
  });

  it("finds a rename", () => {
    const stored = aRepository({ name: "core-extras" });

    expect(repositoryChanges(stored, { ...stored, name: "extras" })).toEqual([
      "rename",
    ]);
  });

  it("treats a change of case as a rename", () => {
    const stored = aRepository({ name: "core-extras" });

    expect(
      repositoryChanges(stored, { ...stored, name: "Core-Extras" }),
    ).toEqual(["rename"]);
  });

  it("finds a change from private to public", () => {
    const stored = aRepository({ isPublic: false });

    expect(repositoryChanges(stored, { ...stored, isPublic: true })).toEqual([
      "makePublic",
    ]);
  });

  it("finds a change from public to private", () => {
    const stored = aRepository({ isPublic: true });

    expect(repositoryChanges(stored, { ...stored, isPublic: false })).toEqual([
      "makePrivate",
    ]);
  });

  it("reads a missing isPublic as private, as the API does", () => {
    const stored = aRepository({ name: "core-extras", isPublic: true });

    expect(repositoryChanges(stored, { name: "core-extras" })).toEqual([
      "makePrivate",
    ]);
    expect(
      repositoryChanges(aRepository({ name: "my-repo", isPublic: false }), {
        name: "my-repo",
      }),
    ).toEqual([]);
  });

  it("lists a rename before a change of visibility", () => {
    const stored = aRepository({ name: "core-extras", isPublic: true });

    expect(
      repositoryChanges(stored, { name: "extras", isPublic: false }),
    ).toEqual(["rename", "makePrivate"]);
  });
});
