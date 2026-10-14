// Turns a request-body property's rules, generated from RepoHost's OpenAPI document, into a validator
// for the form field that edits it. See docs/web-ui-repositories/web-ui-repositories.md, "Validation
// rules from the OpenAPI document".
import { validationRules } from "./validation.gen";
import type { PropertyRules } from "./validationRules";

type Rules = typeof validationRules;

/** A request-body schema the OpenAPI document declares. */
export type ValidatedSchema = keyof Rules;

/** A property of `Schema` the OpenAPI document declares. */
export type ValidatedProperty<Schema extends ValidatedSchema> =
  keyof Rules[Schema] & string;

/** Checks a field's value, returning the message to show or `undefined` when it passes. */
export type FieldValidator<T = string> = (value: T) => string | undefined;

/** How a field's failures are worded. */
export interface MessageOptions {
  /** The field's label, as its messages name it: "Name is required." */
  label: string;
  /**
   * The message for a value failing the property's pattern. The OpenAPI document carries none, so
   * this is the one message a form writes itself.
   */
  patternMessage?: string;
}

/**
 * {@link MessageOptions} for `Property` of `Schema`, with `patternMessage` required exactly when the
 * property has a pattern, whether the browser or only the API checks it.
 */
export type FieldValidatorOptions<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
> = Rules[Schema][Property] extends { pattern: unknown }
  ? { label: string; patternMessage: string }
  : { label: string; patternMessage?: never };

// What RequiredAttribute treats as blank: .NET's char.IsWhiteSpace, less the U+FEFF JavaScript's `\s`
// adds, so the browser never calls a value missing that the API accepts.
const blank = /^[^\S\uFEFF]*$/u;

function characters(count: number): string {
  return `${count} character${count === 1 ? "" : "s"}`;
}

/**
 * The validator for a property with `rules`. A server-only pattern is never checked here; the API's
 * `400` reports it.
 */
export function validatorFor(
  rules: PropertyRules,
  { label, patternMessage }: MessageOptions,
): FieldValidator {
  // DataAnnotations requires the whole value to match, as anchoring the pattern does.
  const pattern =
    rules.pattern?.kind === "exact"
      ? new RegExp(`^(?:${rules.pattern.source})$`, "u")
      : undefined;

  return (value) => {
    if (blank.test(value)) {
      // RequiredAttribute rejects a blank value. An optional one is left to the API, which never
      // makes the browser the stricter of the two.
      return rules.required ? `${label} is required.` : undefined;
    }
    if (rules.minLength !== undefined && value.length < rules.minLength) {
      return `${label} must be at least ${characters(rules.minLength)}.`;
    }
    if (rules.maxLength !== undefined && value.length > rules.maxLength) {
      return `${label} must be ${characters(rules.maxLength)} or fewer.`;
    }
    if (pattern && !pattern.test(value)) {
      return patternMessage ?? `${label} is not valid.`;
    }
    return undefined;
  };
}

/**
 * The validator for `property` of `schema`, by the rules the OpenAPI document gives it. A form names
 * the schema, the property and the field's label, and writes only the pattern's message.
 */
export function fieldValidator<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
>(
  schema: Schema,
  property: Property,
  options: FieldValidatorOptions<Schema, Property>,
): FieldValidator {
  const rules = validationRules[schema][property] as PropertyRules;
  return validatorFor(rules, options);
}
