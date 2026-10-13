// Reads the validation rules RepoHost publishes in its OpenAPI document, for
// scripts/generate-validation.ts to write into src/api/validation.gen.ts. See
// docs/web-ui-repositories/web-ui-repositories.md, "Validation rules from the OpenAPI document".
import { translatePattern } from "./translatePattern.ts";

/** A property's pattern, as the browser may check it. */
export type PatternRule =
  | {
      /** The browser checks the whole value against `source`, a JavaScript `u`-flag pattern. */
      readonly kind: "exact";
      readonly source: string;
    }
  | {
      /** The property has a pattern only the API checks; the browser leaves it to the API's `400`. */
      readonly kind: "server-only";
    };

/** The rules RepoHost validates one request-body property with. */
export interface PropertyRules {
  readonly required: boolean;
  readonly minLength?: number;
  readonly maxLength?: number;
  readonly pattern?: PatternRule;
}

/** Each request-body schema's properties' rules, keyed by schema name and then property name. */
export type ValidationRules = Readonly<
  Record<string, Readonly<Record<string, PropertyRules>>>
>;

/** The rules, and a warning for each pattern the browser cannot check. */
export interface ExtractedValidationRules {
  rules: ValidationRules;
  warnings: string[];
}

interface SchemaObject {
  required?: string[];
  properties?: Record<string, PropertyObject>;
}

interface PropertyObject {
  minLength?: number;
  maxLength?: number;
  pattern?: string;
}

/** The parts of an OpenAPI 3 document the rules are read from. */
export interface ValidationDocument {
  paths?: Record<string, Record<string, unknown> | undefined>;
  components?: { schemas?: Record<string, SchemaObject> };
}

const schemaRefPrefix = "#/components/schemas/";

/** The names of the component schemas any operation in `document` takes as its request body. */
export function requestBodySchemaNames(document: ValidationDocument): string[] {
  const names = new Set<string>();
  for (const pathItem of Object.values(document.paths ?? {})) {
    for (const operation of Object.values(pathItem ?? {})) {
      const content = (
        operation as {
          requestBody?: {
            content?: Record<string, { schema?: { $ref?: string } }>;
          };
        } | null
      )?.requestBody?.content;
      for (const media of Object.values(content ?? {})) {
        const ref = media.schema?.$ref;
        if (ref?.startsWith(schemaRefPrefix)) {
          names.add(ref.slice(schemaRefPrefix.length));
        }
      }
    }
  }
  return [...names].sort();
}

/**
 * Every request-body schema's properties' `required`, `minLength`, `maxLength` and `pattern`, with
 * each pattern translated for the browser where the translator recognises all of it, and server-only
 * with a warning where it does not. Never throws over a pattern.
 */
export function extractValidationRules(
  document: ValidationDocument,
): ExtractedValidationRules {
  const warnings: string[] = [];
  const rules: Record<string, Record<string, PropertyRules>> = {};

  for (const schemaName of requestBodySchemaNames(document)) {
    const schema = document.components?.schemas?.[schemaName];
    if (!schema) {
      continue;
    }
    const required = new Set(schema.required ?? []);
    const properties: Record<string, PropertyRules> = {};
    for (const [propertyName, property] of Object.entries(
      schema.properties ?? {},
    )) {
      let pattern: PatternRule | undefined;
      if (property.pattern !== undefined) {
        const translation = translatePattern(property.pattern);
        if (translation.kind === "exact") {
          pattern = { kind: "exact", source: translation.source };
        } else {
          pattern = { kind: "server-only" };
          warnings.push(
            `${schemaName}.${propertyName}: the pattern ${JSON.stringify(property.pattern)} is checked by the server only, ` +
              `because the translator does not recognise ${JSON.stringify(translation.construct)}.`,
          );
        }
      }
      properties[propertyName] = {
        required: required.has(propertyName),
        ...(property.minLength !== undefined && {
          minLength: property.minLength,
        }),
        ...(property.maxLength !== undefined && {
          maxLength: property.maxLength,
        }),
        ...(pattern && { pattern }),
      };
    }
    rules[schemaName] = properties;
  }

  return { rules, warnings };
}

/** The source of `src/api/validation.gen.ts` holding `rules`. */
export function renderValidationModule(rules: ValidationRules): string {
  return (
    `// Generated from openapi/v1.json by scripts/generate-validation.ts. Do not edit.\n\n` +
    `import type { ValidationRules } from "./validationRules.ts";\n\n` +
    `export const validationRules = ${JSON.stringify(rules, null, 2)} as const satisfies ValidationRules;\n`
  );
}
