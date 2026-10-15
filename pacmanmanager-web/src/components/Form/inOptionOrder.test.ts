import { describe, expect, it } from "vitest";
import { inOptionOrder } from "./inOptionOrder";

const options = [{ value: "x86_64" }, { value: "aarch64" }, { value: "i686" }];

describe("inOptionOrder", () => {
  it("orders the selected values as the options are ordered", () => {
    expect(inOptionOrder(["i686", "x86_64"], options)).toEqual([
      "x86_64",
      "i686",
    ]);
  });

  it("keeps values not among the options after the rest, in their own order", () => {
    expect(
      inOptionOrder(["riscv64", "i686", "armv7h", "x86_64"], options),
    ).toEqual(["x86_64", "i686", "riscv64", "armv7h"]);
  });

  it("returns nothing for nothing selected", () => {
    expect(inOptionOrder([], options)).toEqual([]);
  });

  it("returns the values as given when there are no options", () => {
    expect(inOptionOrder(["i686", "x86_64"], [])).toEqual(["i686", "x86_64"]);
  });
});
