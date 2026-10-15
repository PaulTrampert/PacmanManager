import { describe, expect, it } from "vitest";
import { aRepository } from "../../mocks/fixtures";
import { pacmanConfSectionText } from "./pacmanConfSectionText";

describe("pacmanConfSectionText", () => {
  it("builds the section from the repository's name and serverUrl", () => {
    const repository = aRepository({
      name: "core-extras",
      serverUrl: "https://packages.example.com/pacman/$repo/$arch",
    });

    expect(pacmanConfSectionText(repository)).toBe(
      "[core-extras]\n" +
        "SigLevel = Optional TrustAll\n" +
        "Server = https://packages.example.com/pacman/$repo/$arch",
    );
  });

  it("uses the name as stored and the serverUrl verbatim", () => {
    const repository = aRepository({
      name: "My_Repo.2",
      serverUrl: "http://10.0.0.5:8082/pacman/$repo/$arch?x=1",
    });

    expect(pacmanConfSectionText(repository)).toBe(
      "[My_Repo.2]\n" +
        "SigLevel = Optional TrustAll\n" +
        "Server = http://10.0.0.5:8082/pacman/$repo/$arch?x=1",
    );
  });
});
