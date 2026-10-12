import { describe, expect, it } from "vitest";
import { ApiError, isValidationProblemDetails } from "./ApiError";

describe("ApiError", () => {
  it("takes its message from the problem's title", () => {
    const error = new ApiError(409, { title: "Conflict" });

    expect(error.message).toBe("Conflict");
    expect(error.name).toBe("ApiError");
  });

  it.each([
    ["no body", undefined],
    ["a text body", "Bad Gateway"],
    ["an array body", ["oops"]],
  ])("keeps the status of a response with %s", (_, body) => {
    const error = new ApiError(502, body);

    expect(error.status).toBe(502);
    expect(error.problem).toBeUndefined();
    expect(error.title).toBeUndefined();
    expect(error.message).toBe("The request failed with status 502.");
  });

  it("treats a null title as no title", () => {
    expect(new ApiError(500, { title: null }).title).toBeUndefined();
  });

  it("has no errors when the problem is not a validation problem", () => {
    expect(new ApiError(400, { title: "Bad", errors: "nope" }).errors).toBe(
      undefined,
    );
  });
});

describe("isValidationProblemDetails", () => {
  it("accepts errors mapping fields to messages", () => {
    expect(
      isValidationProblemDetails({
        errors: { Name: ["Required."], Other: [] },
      }),
    ).toBe(true);
  });

  it.each([
    ["no errors", {}],
    ["errors that are not an object", { errors: ["Required."] }],
    [
      "a field whose messages are not a list",
      { errors: { Name: "Required." } },
    ],
    ["a message that is not a string", { errors: { Name: [1] } }],
    ["a body that is not an object", "Required."],
  ])("rejects %s", (_, problem) => {
    expect(isValidationProblemDetails(problem)).toBe(false);
  });
});
