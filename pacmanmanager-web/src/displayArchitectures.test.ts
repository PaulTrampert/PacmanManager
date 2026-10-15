import { describe, expect, it } from "vitest";
import { displayArchitectures } from "./displayArchitectures";
import { anArchitecture } from "./mocks/fixtures";

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
  anArchitecture({ value: "aarch64", displayValue: "64-bit ARM (aarch64)" }),
];

describe("displayArchitectures", () => {
  it("shows each known architecture by its display value, in the repository's order", () => {
    expect(displayArchitectures(["aarch64", "x86_64"], architectures)).toEqual([
      "64-bit ARM (aarch64)",
      "64-bit Intel/AMD (x86_64)",
    ]);
  });

  it("falls back to the raw value for an architecture not in the array", () => {
    expect(displayArchitectures(["riscv64", "x86_64"], architectures)).toEqual([
      "riscv64",
      "64-bit Intel/AMD (x86_64)",
    ]);
  });

  it("shows raw values while there are no architectures to look them up in", () => {
    expect(displayArchitectures(["x86_64"], [])).toEqual(["x86_64"]);
  });

  it("shows nothing for a repository with no architectures", () => {
    expect(displayArchitectures([], architectures)).toEqual([]);
  });
});
