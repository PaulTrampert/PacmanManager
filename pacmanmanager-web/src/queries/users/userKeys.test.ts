import { describe, expect, it } from "vitest";
import { userKeys } from "./userKeys";

describe("userKeys", () => {
  it("starts every key with the resource name", () => {
    expect(userKeys.all).toEqual(["users"]);
    expect(userKeys.lists().slice(0, 1)).toEqual(userKeys.all);
    expect(userKeys.list({}).slice(0, 1)).toEqual(userKeys.all);
    expect(userKeys.detail("a").slice(0, 1)).toEqual(userKeys.all);
  });

  it("keys a list by the list key and its search", () => {
    const search = { displayNameContains: "ada", pageSize: 20 };

    expect(userKeys.list(search)).toEqual(["users", "list", search]);
  });

  it("keys a detail by the detail key and its id", () => {
    expect(userKeys.detail("abc")).toEqual(["users", "detail", "abc"]);
  });
});
