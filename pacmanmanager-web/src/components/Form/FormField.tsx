import type { ReactNode } from "react";
import {
  fieldValidator,
  type FieldValidatorOptions,
  type ValidatedProperty,
  type ValidatedSchema,
} from "../../api";
import { FormTextField } from "./FormTextField";

/** The props {@link FormField} takes. */
export type FormFieldProps<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
> = {
  /** The request-body schema the field's value is sent in, as the OpenAPI document names it. */
  schema: Schema;
  /** The property of `schema` the field edits, which also names the field. */
  property: Property;
  /** The field's current value. */
  value: string;
  /** Called with the new value as it is edited. */
  onChange: (value: string) => void;
  /** Shown below the field while it has no error to show. */
  helperText?: ReactNode;
  /** The API's message for the field, from the last request's validation `400` or a `409`. */
  serverError?: string;
  /** Disables the field. */
  disabled?: boolean;
} & FieldValidatorOptions<Schema, Property>;

/**
 * An outlined text field inside a `Form`, validated by the rules the OpenAPI document gives
 * `property` of `schema`. Its error replaces its helper text once the field has been left or the
 * form submitted, and a failing field stops the form submitting. A form names the schema, the
 * property and the label, and words only a pattern's message; it never states a length or a
 * pattern itself.
 */
export function FormField<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
>({
  schema,
  property,
  label,
  patternMessage,
  ...field
}: FormFieldProps<Schema, Property>) {
  const options = { label, patternMessage } as FieldValidatorOptions<
    Schema,
    Property
  >;
  return (
    <FormTextField
      name={property}
      label={label}
      validate={fieldValidator(schema, property, options)}
      {...field}
    />
  );
}
