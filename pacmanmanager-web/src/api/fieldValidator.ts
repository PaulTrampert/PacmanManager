// Turns a request-body property's rules, generated from RepoHost's OpenAPI document, into a validator
// for the form field that edits it. See docs/web-ui-repositories/web-ui-repositories.md, "Validation
// rules from the OpenAPI document".
import type { components } from "./schema.gen";
import { validationRules } from "./validation.gen";
import type { PropertyRules } from "./validationRules";

type Rules = typeof validationRules;
type Schemas = components["schemas"];

/** A request-body schema the OpenAPI document declares. */
export type ValidatedSchema = keyof Rules;

/** A property of `Schema` the OpenAPI document declares. */
export type ValidatedProperty<Schema extends ValidatedSchema> =
  keyof Rules[Schema] & string;

/** A value a field may hold: what the OpenAPI document gives a request-body property. */
export type FieldValue = string | boolean | readonly (string | boolean)[];

/**
 * The type of `Property` of `Schema` in the generated API schema, without the `null` or `undefined`
 * an optional property allows: the value a field editing it holds.
 */
export type PropertyValue<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
> = Schema extends keyof Schemas
  ? Property extends keyof Schemas[Schema]
    ? NonNullable<Schemas[Schema][Property]>
    : never
  : never;

/** Checks a field's value, returning every failing rule's message, none when it passes. */
export type FieldValidator<T = string> = (value: T) => string[];

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

function counted(count: number, noun: string): string {
  return `${count} ${noun}${count === 1 ? "" : "s"}`;
}

/** Whether `value` is missing, as `required` sees it: a blank string or an empty array. */
function isBlank(value: FieldValue): boolean {
  if (typeof value === "string") {
    return blank.test(value);
  }
  if (Array.isArray(value)) {
    return value.length === 0;
  }
  // A boolean is always present.
  return false;
}

/**
 * The validator for a property with `rules`, of whichever type the property is. A server-only
 * pattern is never checked here; the API's `400` reports it.
 */
export function validatorFor<T extends FieldValue = string>(
  rules: PropertyRules,
  { label, patternMessage }: MessageOptions,
): FieldValidator<T> {
  // DataAnnotations requires the whole value to match, as anchoring the pattern does.
  const pattern =
    rules.pattern?.kind === "exact"
      ? new RegExp(`^(?:${rules.pattern.source})$`, "u")
      : undefined;

  return (value) => {
    if (isBlank(value)) {
      // RequiredAttribute rejects a blank value, and no other rule applies to a missing one. An
      // optional one is left to the API, which never makes the browser the stricter of the two.
      return rules.required ? [`${label} is required.`] : [];
    }
    const messages: string[] = [];
    if (typeof value === "string") {
      if (rules.minLength !== undefined && value.length < rules.minLength) {
        messages.push(
          `${label} must be at least ${counted(rules.minLength, "character")}.`,
        );
      }
      if (rules.maxLength !== undefined && value.length > rules.maxLength) {
        messages.push(
          `${label} must be ${counted(rules.maxLength, "character")} or fewer.`,
        );
      }
      if (pattern && !pattern.test(value)) {
        messages.push(patternMessage ?? `${label} is not valid.`);
      }
    } else if (Array.isArray(value)) {
      if (rules.minItems !== undefined && value.length < rules.minItems) {
        messages.push(
          `${label} must have at least ${counted(rules.minItems, "item")}.`,
        );
      }
      if (rules.maxItems !== undefined && value.length > rules.maxItems) {
        messages.push(
          `${label} must have ${counted(rules.maxItems, "item")} or fewer.`,
        );
      }
    }
    return messages;
  };
}

/**
 * The validator for `property` of `schema`, by the rules the OpenAPI document gives it, checking a
 * value of the property's type. A form names the schema, the property and the field's label, and
 * writes only the pattern's message.
 */
export function fieldValidator<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
>(
  schema: Schema,
  property: Property,
  options: FieldValidatorOptions<Schema, Property>,
): FieldValidator<PropertyValue<Schema, Property>> {
  const rules = validationRules[schema][property] as PropertyRules;
  // Every property's type in the generated schema is a FieldValue, which TypeScript cannot see
  // through the generic PropertyValue.
  return validatorFor<FieldValue>(rules, options) as FieldValidator<
    PropertyValue<Schema, Property>
  >;
}
