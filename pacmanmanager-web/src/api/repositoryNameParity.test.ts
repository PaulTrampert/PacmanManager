// The browser's check of a repository name, generated from the OpenAPI document, agrees with
// RepoHost's pattern on every case in the fixture RepoHost's RegularExpressionsTests read too.
import { describe, expect, it } from "vitest";
import fixture from "../../../test-fixtures/validation/repository-names.json";
import { validatorFor } from "./fieldValidator";
import { validationRules } from "./validation.gen";

const rules = validationRules.WriteRepositoryRequest.name;
const patternMessage = "pattern";
const validate = validatorFor(
  // Only the pattern: the fixture's cases are about it, not about length or presence.
  { required: false, pattern: rules.pattern },
  { label: "Name", patternMessage },
);

describe("WriteRepositoryRequest.name", () => {
  it("has a pattern the browser checks exactly", () => {
    expect(rules.pattern.kind).toBe("exact");
  });

  it.each(fixture.accepted)("accepts %j", (name) => {
    expect(validate(name)).toEqual([]);
  });

  it.each(fixture.rejected)("rejects %j", (name) => {
    expect(validate(name)).toEqual([patternMessage]);
  });
});
