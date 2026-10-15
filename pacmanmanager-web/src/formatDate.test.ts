import { describe, expect, it } from "vitest";
import { formatDate } from "./formatDate";

describe("formatDate", () => {
  it("formats a date-time as a medium-length date", () => {
    // Midday UTC is the same date in every time zone the tests could run in.
    expect(formatDate("2026-03-02T12:00:00Z", "en-US")).toBe("Mar 2, 2026");
  });

  it("follows the locale it is given", () => {
    expect(formatDate("2026-03-02T12:00:00Z", "en-GB")).toBe("2 Mar 2026");
  });
});
