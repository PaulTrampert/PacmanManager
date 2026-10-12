import { renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ApiError } from "./api";
import { mapFormErrors, useFormErrors } from "./useFormErrors";

const fields = ["name", "supportedArchitectures"] as const;

function validationError(errors: Record<string, string[]>) {
  return new ApiError(400, {
    title: "One or more validation errors occurred.",
    status: 400,
    errors,
  });
}

describe("mapFormErrors", () => {
  it("puts each entry on the field it names, ignoring case", () => {
    const error = validationError({
      Name: ["The Name field is required."],
      SupportedArchitectures: ["At least one architecture is required."],
    });

    expect(mapFormErrors(error, fields)).toEqual({
      fieldErrors: {
        name: "The Name field is required.",
        supportedArchitectures: "At least one architecture is required.",
      },
      otherErrors: [],
    });
  });

  it("matches an entry named with a prefix by its last segment", () => {
    const error = validationError({
      "$.name": ["The JSON value could not be converted."],
      "request.Name": ["The Name field is required."],
    });

    expect(mapFormErrors(error, fields).fieldErrors).toEqual({
      name: "The JSON value could not be converted. The Name field is required.",
    });
  });

  it("joins a field's messages into one", () => {
    const error = validationError({
      Name: ["The name is too long.", "The name has invalid characters."],
    });

    expect(mapFormErrors(error, fields).fieldErrors.name).toBe(
      "The name is too long. The name has invalid characters.",
    );
  });

  it("leaves entries naming no field for the alert above the form", () => {
    const error = validationError({
      Name: ["The Name field is required."],
      "": ["A non-empty request body is required."],
      Upload: ["The file is not a pacman package."],
    });

    expect(mapFormErrors(error, fields)).toEqual({
      fieldErrors: { name: "The Name field is required." },
      otherErrors: [
        "A non-empty request body is required.",
        "The file is not a pacman package.",
      ],
    });
  });

  it.each([
    ["no error", undefined],
    [
      "an ApiError without validation errors",
      new ApiError(409, { title: "Conflict" }),
    ],
    ["a network failure", new TypeError("Failed to fetch")],
  ])("maps %s to nothing", (_, error) => {
    expect(mapFormErrors(error, fields)).toEqual({
      fieldErrors: {},
      otherErrors: [],
    });
  });
});

describe("useFormErrors", () => {
  it("maps the error as mapFormErrors does", () => {
    const error = validationError({
      Name: ["The Name field is required."],
      Other: ["Something else."],
    });

    const { result } = renderHook(() => useFormErrors(error, fields));

    expect(result.current).toEqual(mapFormErrors(error, fields));
  });
});
