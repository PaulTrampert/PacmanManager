import { describe, expect, it } from "vitest";
import {
  extractValidationRules,
  renderValidationModule,
  requestBodySchemaNames,
  type ValidationDocument,
} from "./validationRules";

function bodyOf(schemaName: string) {
  return {
    requestBody: {
      content: {
        "application/json": {
          schema: { $ref: `#/components/schemas/${schemaName}` },
        },
      },
    },
  };
}

function documentWith(
  properties: Record<string, object>,
  required: string[] = [],
): ValidationDocument {
  return {
    paths: { "/api/v1/Things": { post: bodyOf("WriteThing") } },
    components: {
      schemas: { WriteThing: { required, properties } },
    },
  };
}

describe("requestBodySchemaNames", () => {
  it("names each schema an operation takes as its body, once, and no other", () => {
    const document: ValidationDocument = {
      paths: {
        "/api/v1/Things": {
          get: {},
          post: bodyOf("WriteThing"),
          parameters: [],
        },
        "/api/v1/Things/{id}": { put: bodyOf("WriteThing") },
        "/api/v1/Others": { patch: bodyOf("PatchOther") },
      },
      components: {
        schemas: { WriteThing: {}, PatchOther: {}, Thing: {} },
      },
    };

    expect(requestBodySchemaNames(document)).toEqual([
      "PatchOther",
      "WriteThing",
    ]);
  });
});

describe("extractValidationRules", () => {
  it("takes required, minLength and maxLength as they are", () => {
    const { rules, warnings } = extractValidationRules(
      documentWith(
        {
          name: { type: "string", minLength: 1, maxLength: 255 },
          note: { type: "string", maxLength: 10 },
          flag: { type: "boolean" },
        },
        ["name"],
      ),
    );

    expect(rules).toEqual({
      WriteThing: {
        name: { required: true, minLength: 1, maxLength: 255 },
        note: { required: false, maxLength: 10 },
        flag: { required: false },
      },
    });
    expect(warnings).toEqual([]);
  });

  it("translates a pattern it recognises into an exact rule", () => {
    const { rules, warnings } = extractValidationRules(
      documentWith({ name: { type: "string", pattern: "\\A[a-z]+\\z" } }),
    );

    expect(rules.WriteThing.name.pattern).toEqual({
      kind: "exact",
      source: "^[a-z]+$",
    });
    expect(warnings).toEqual([]);
  });

  it.each([
    ["\\A\\d+\\z", "\\d"],
    ["\\A\\w+\\z", "\\w"],
    ["\\A\\s+\\z", "\\s"],
    ["\\Aa\\b", "\\b"],
    ["\\A.+\\z", "."],
    ["^a+$", "$"],
    ["\\Aa+\\Z", "\\Z"],
    ["(?i)\\Aa+\\z", "(?i)"],
    ["\\A[\\w-]+\\z", "\\w"],
  ])(
    "makes %s server-only, warning that it does not recognise %s",
    (pattern, construct) => {
      const { rules, warnings } = extractValidationRules(
        documentWith({ name: { type: "string", pattern } }),
      );

      expect(rules.WriteThing.name.pattern).toEqual({ kind: "server-only" });
      expect(warnings).toHaveLength(1);
      expect(warnings[0]).toContain("WriteThing.name");
      expect(warnings[0]).toContain(JSON.stringify(construct));
    },
  );

  it("never carries a server-only pattern's source", () => {
    const { rules } = extractValidationRules(
      documentWith({ name: { type: "string", pattern: "\\A\\d+\\z" } }),
    );

    expect(JSON.stringify(rules)).not.toContain("\\\\d");
  });
});

describe("renderValidationModule", () => {
  it("writes the rules as a constant checked against their type", () => {
    const source = renderValidationModule({
      WriteThing: { name: { required: true, maxLength: 3 } },
    });

    expect(source).toMatch(/^\/\/ Generated from openapi\/v1\.json/);
    expect(source).toContain(
      'import type { ValidationRules } from "./validationRules.ts";',
    );
    expect(source).toContain("export const validationRules = {");
    expect(source).toContain('"maxLength": 3');
    expect(source).toContain("} as const satisfies ValidationRules;");
  });
});
