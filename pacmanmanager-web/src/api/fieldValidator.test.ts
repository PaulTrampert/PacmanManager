import { describe, expect, it } from "vitest";
import { fieldValidator, validatorFor } from "./fieldValidator";
import { validationRules } from "./validation.gen";

const label = { label: "Name" };

describe("validatorFor", () => {
  describe("required", () => {
    const validate = validatorFor({ required: true, minLength: 1 }, label);

    it.each(["", "   ", "\t\n"])("rejects %j as missing", (value) => {
      expect(validate(value)).toBe("Name is required.");
    });

    it("accepts a value", () => {
      expect(validate("core")).toBeUndefined();
    });

    it("does not call a value of U+FEFF missing, as the API does not", () => {
      expect(validate("\uFEFF")).toBeUndefined();
    });

    it("leaves an optional blank value alone", () => {
      expect(
        validatorFor(
          { required: false, minLength: 1, maxLength: 1 },
          label,
        )(""),
      ).toBeUndefined();
    });
  });

  describe("lengths", () => {
    const validate = validatorFor(
      { required: true, minLength: 3, maxLength: 5 },
      label,
    );

    it("rejects a value shorter than minLength", () => {
      expect(validate("ab")).toBe("Name must be at least 3 characters.");
    });

    it("rejects a value longer than maxLength", () => {
      expect(validate("abcdef")).toBe("Name must be 5 characters or fewer.");
    });

    it.each(["abc", "abcde"])("accepts %s, within both", (value) => {
      expect(validate(value)).toBeUndefined();
    });

    it("names a single character in the singular", () => {
      expect(validatorFor({ required: true, maxLength: 1 }, label)("ab")).toBe(
        "Name must be 1 character or fewer.",
      );
    });
  });

  describe("pattern", () => {
    const patternMessage = "Use lowercase letters.";

    it("rejects a value an exact pattern does not match, with the form's message", () => {
      const validate = validatorFor(
        { required: true, pattern: { kind: "exact", source: "[a-z]+" } },
        { label: "Name", patternMessage },
      );

      expect(validate("Core")).toBe(patternMessage);
      expect(validate("core")).toBeUndefined();
    });

    it("matches the whole value, as DataAnnotations does, even when the pattern is unanchored", () => {
      const validate = validatorFor(
        { required: true, pattern: { kind: "exact", source: "[a-z]+" } },
        { label: "Name", patternMessage },
      );

      expect(validate("core1")).toBe(patternMessage);
    });

    it("never checks a server-only pattern", () => {
      const validate = validatorFor(
        { required: true, pattern: { kind: "server-only" } },
        { label: "Name", patternMessage },
      );

      expect(validate("anything at all ¯\\_(ツ)_/¯")).toBeUndefined();
    });

    it("reports a length failure before a pattern failure", () => {
      const validate = validatorFor(
        {
          required: true,
          maxLength: 2,
          pattern: { kind: "exact", source: "[a-z]+" },
        },
        { label: "Name", patternMessage },
      );

      expect(validate("ABC")).toBe("Name must be 2 characters or fewer.");
    });
  });
});

describe("fieldValidator", () => {
  const patternMessage = "Use letters, digits and @ _ + - .";
  const validate = fieldValidator("WriteRepositoryRequest", "name", {
    label: "Name",
    patternMessage,
  });

  it("applies the document's rules to the named property", () => {
    const maxLength = validationRules.WriteRepositoryRequest.name.maxLength;

    expect(validate("")).toBe("Name is required.");
    expect(validate("a".repeat(maxLength + 1))).toBe(
      `Name must be ${maxLength} characters or fewer.`,
    );
    expect(validate("-core")).toBe(patternMessage);
    expect(validate("core")).toBeUndefined();
  });

  it("refers only to schemas and properties the document declares", () => {
    // Each of these is a type error, which `npm run typecheck` reports: the unused directive would
    // fail it if one ever stopped being one.
    expect(() =>
      // @ts-expect-error -- no such schema
      fieldValidator("NoSuchRequest", "name", { label: "Name" }),
    ).toThrow();
    expect(() =>
      // @ts-expect-error -- no such property
      fieldValidator("WriteRepositoryRequest", "noSuchProperty", {
        label: "Name",
        patternMessage,
      }),
    ).toThrow();
  });

  it("requires a pattern message exactly where the property has a pattern", () => {
    // @ts-expect-error -- `name` has a pattern, so its message is required
    fieldValidator("WriteRepositoryRequest", "name", { label: "Name" });
    fieldValidator("WriteUserRequest", "displayName", {
      label: "Display name",
    });
    fieldValidator("WriteUserRequest", "displayName", {
      label: "Display name",
      // @ts-expect-error -- `displayName` has no pattern to word a message for
      patternMessage,
    });
  });
});
