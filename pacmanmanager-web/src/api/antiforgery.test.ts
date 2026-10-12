import { describe, expect, it } from "vitest";
import { readCookie } from "./antiforgery";

describe("readCookie", () => {
  it("finds the named cookie among others", () => {
    expect(readCookie("a=1; XSRF-TOKEN=abc; b=2", "XSRF-TOKEN")).toBe("abc");
  });

  it("decodes the value", () => {
    expect(readCookie("XSRF-TOKEN=a%2Bb%3D", "XSRF-TOKEN")).toBe("a+b=");
  });

  it("keeps an equals sign inside the value", () => {
    expect(readCookie("XSRF-TOKEN=abc==", "XSRF-TOKEN")).toBe("abc==");
  });

  it.each([
    ["no cookies", ""],
    ["only other cookies", "a=1; b=2"],
    ["a cookie whose name merely ends the same", "OLD-XSRF-TOKEN=abc"],
  ])("is undefined for %s", (_, cookies) => {
    expect(readCookie(cookies, "XSRF-TOKEN")).toBeUndefined();
  });
});
