import { describe, expect, expectTypeOf, it } from "vitest";
import {
  fieldValidator,
  validatorFor,
  type FieldValidator,
} from "./fieldValidator";
import { validationRules } from "./validation.gen";

const label = { label: "Name" };

describe("validatorFor", () => {
  describe("a string", () => {
    describe("required", () => {
      const validate = validatorFor({ required: true, minLength: 1 }, label);

      it.each(["", "   ", "\t\n"])("rejects %j as missing", (value) => {
        expect(validate(value)).toEqual(["Name is required."]);
      });

      it("accepts a value", () => {
        expect(validate("core")).toEqual([]);
      });

      it("does not call a value of U+FEFF missing, as the API does not", () => {
        expect(validate("﻿")).toEqual([]);
      });

      it("leaves an optional blank value alone", () => {
        expect(
          validatorFor(
            { required: false, minLength: 1, maxLength: 1 },
            label,
          )(""),
        ).toEqual([]);
      });
    });

    describe("lengths", () => {
      const validate = validatorFor(
        { required: true, minLength: 3, maxLength: 5 },
        label,
      );

      it("rejects a value shorter than minLength", () => {
        expect(validate("ab")).toEqual(["Name must be at least 3 characters."]);
      });

      it("rejects a value longer than maxLength", () => {
        expect(validate("abcdef")).toEqual([
          "Name must be 5 characters or fewer.",
        ]);
      });

      it.each(["abc", "abcde"])("accepts %s, within both", (value) => {
        expect(validate(value)).toEqual([]);
      });

      it("names a single character in the singular", () => {
        expect(
          validatorFor({ required: true, maxLength: 1 }, label)("ab"),
        ).toEqual(["Name must be 1 character or fewer."]);
      });
    });

    describe("pattern", () => {
      const patternMessage = "Use lowercase letters.";

      it("rejects a value an exact pattern does not match, with the form's message", () => {
        const validate = validatorFor(
          { required: true, pattern: { kind: "exact", source: "[a-z]+" } },
          { label: "Name", patternMessage },
        );

        expect(validate("Core")).toEqual([patternMessage]);
        expect(validate("core")).toEqual([]);
      });

      it("matches the whole value, as DataAnnotations does, even when the pattern is unanchored", () => {
        const validate = validatorFor(
          { required: true, pattern: { kind: "exact", source: "[a-z]+" } },
          { label: "Name", patternMessage },
        );

        expect(validate("core1")).toEqual([patternMessage]);
      });

      it("never checks a server-only pattern", () => {
        const validate = validatorFor(
          { required: true, pattern: { kind: "server-only" } },
          { label: "Name", patternMessage },
        );

        expect(validate("anything at all ¯\\_(ツ)_/¯")).toEqual([]);
      });

      it("reports both a length failure and a pattern failure", () => {
        const validate = validatorFor(
          {
            required: true,
            maxLength: 2,
            pattern: { kind: "exact", source: "[a-z]+" },
          },
          { label: "Name", patternMessage },
        );

        expect(validate("ABC")).toEqual([
          "Name must be 2 characters or fewer.",
          patternMessage,
        ]);
      });
    });

    it("does not apply an array's rules", () => {
      expect(
        validatorFor(
          { required: true, minItems: 5, maxItems: 0 },
          label,
        )("core"),
      ).toEqual([]);
    });
  });

  describe("an array", () => {
    const architectures = { label: "Architectures" };

    describe.each([
      ["strings", ["x86_64", "aarch64", "armv7h"]],
      ["booleans", [true, false, true]],
    ] as const)("of %s", (_, items) => {
      describe("required", () => {
        const validate = validatorFor<readonly (string | boolean)[]>(
          { required: true },
          architectures,
        );

        it("rejects an empty array as missing", () => {
          expect(validate([])).toEqual(["Architectures is required."]);
        });

        it("accepts an array with an item", () => {
          expect(validate(items.slice(0, 1))).toEqual([]);
        });

        it("leaves an optional empty array alone", () => {
          expect(
            validatorFor<readonly (string | boolean)[]>(
              { required: false, minItems: 2 },
              architectures,
            )([]),
          ).toEqual([]);
        });
      });

      describe("items", () => {
        const validate = validatorFor<readonly (string | boolean)[]>(
          { required: true, minItems: 2, maxItems: 2 },
          architectures,
        );

        it("rejects an array with fewer than minItems", () => {
          expect(validate(items.slice(0, 1))).toEqual([
            "Architectures must have at least 2 items.",
          ]);
        });

        it("rejects an array with more than maxItems", () => {
          expect(validate(items)).toEqual([
            "Architectures must have 2 items or fewer.",
          ]);
        });

        it("accepts an array within both", () => {
          expect(validate(items.slice(0, 2))).toEqual([]);
        });

        it("names a single item in the singular", () => {
          expect(
            validatorFor<readonly (string | boolean)[]>(
              { required: true, maxItems: 1 },
              architectures,
            )(items.slice(0, 2)),
          ).toEqual(["Architectures must have 1 item or fewer."]);
        });

        it("reports every failing rule", () => {
          // Rules no document would write together, so that one value fails both.
          expect(
            validatorFor<readonly (string | boolean)[]>(
              { required: true, minItems: 4, maxItems: 2 },
              architectures,
            )(items),
          ).toEqual([
            "Architectures must have at least 4 items.",
            "Architectures must have 2 items or fewer.",
          ]);
        });
      });
    });

    it("does not apply a string's rules", () => {
      expect(
        validatorFor<readonly string[]>(
          {
            required: true,
            minLength: 5,
            maxLength: 0,
            pattern: { kind: "exact", source: "[0-9]+" },
          },
          { label: "Architectures", patternMessage: "Digits." },
        )(["x86_64"]),
      ).toEqual([]);
    });
  });

  describe("a boolean", () => {
    it.each([true, false])(
      "is never missing, so %s passes required",
      (value) => {
        expect(
          validatorFor<boolean>(
            { required: true },
            { label: "Visibility" },
          )(value),
        ).toEqual([]);
      },
    );

    it("does not apply a string's or an array's rules", () => {
      expect(
        validatorFor<boolean>(
          {
            required: true,
            minLength: 5,
            maxLength: 0,
            minItems: 5,
            maxItems: 0,
            pattern: { kind: "exact", source: "[0-9]+" },
          },
          { label: "Visibility", patternMessage: "Digits." },
        )(true),
      ).toEqual([]);
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

    expect(validate("")).toEqual(["Name is required."]);
    expect(validate("a".repeat(maxLength + 1))).toEqual([
      `Name must be ${maxLength} characters or fewer.`,
    ]);
    expect(validate("-core")).toEqual([patternMessage]);
    expect(validate("core")).toEqual([]);
  });

  it("gives every message for a value failing two of the document's rules", () => {
    const maxLength = validationRules.WriteRepositoryRequest.name.maxLength;

    expect(validate(`-${"a".repeat(maxLength)}`)).toEqual([
      `Name must be ${maxLength} characters or fewer.`,
      patternMessage,
    ]);
  });

  it("checks a value of the property's type", () => {
    expectTypeOf(validate).toEqualTypeOf<FieldValidator<string>>();

    const architectures = fieldValidator(
      "WriteRepositoryRequest",
      "supportedArchitectures",
      { label: "Architectures" },
    );
    expectTypeOf(architectures).toEqualTypeOf<FieldValidator<string[]>>();
    expect(architectures([])).toEqual(["Architectures is required."]);
    expect(architectures(["x86_64"])).toEqual([]);

    const visibility = fieldValidator("WriteRepositoryRequest", "isPublic", {
      label: "Visibility",
    });
    expectTypeOf(visibility).toEqualTypeOf<FieldValidator<boolean>>();
    expect(visibility(false)).toEqual([]);

    // A nullable property's field holds a value, never null.
    expectTypeOf(
      fieldValidator("CreateAccessTokenRequest", "expiresAt", {
        label: "Expires",
      }),
    ).toEqualTypeOf<FieldValidator<string>>();
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
