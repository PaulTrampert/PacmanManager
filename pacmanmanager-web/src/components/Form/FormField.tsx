import type { ComponentType, ReactNode } from "react";
import {
  fieldValidator,
  type FieldValidatorOptions,
  type ValidatedProperty,
  type ValidatedSchema,
} from "../../api";
import type { FormControlProps } from "./FormControlProps";
import { FormTextField, type FormTextFieldProps } from "./FormTextField";

/** The props {@link FormField} takes. */
export type FormFieldProps<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
  Props extends FormControlProps<string> = FormTextFieldProps,
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
  /**
   * The control that renders the field, a `Form<Type>Field`; {@link FormTextField} when left out.
   * Any props it takes beyond {@link FormControlProps} are passed to it.
   */
  control?: ComponentType<Props>;
} & FieldValidatorOptions<Schema, Property> &
  // Props is inferred from `control` alone, and is FormTextFieldProps without one.
  NoInfer<Omit<Props, keyof FormControlProps<string>>>;

/**
 * A field inside a `Form`, validated by the rules the OpenAPI document gives `property` of `schema`
 * and rendered by `control`, an outlined text field by default. Its error replaces its helper text
 * once the field has been left or the form submitted, and a failing field stops the form
 * submitting. A form names the schema, the property and the label, and words only a pattern's
 * message; it never states a length or a pattern itself.
 */
export function FormField<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
  Props extends FormControlProps<string> = FormTextFieldProps,
>({
  schema,
  property,
  label,
  patternMessage,
  control,
  ...field
}: FormFieldProps<Schema, Property, Props>) {
  const options = { label, patternMessage } as FieldValidatorOptions<
    Schema,
    Property
  >;
  // Without a control Props is FormTextFieldProps, so FormTextField takes them.
  const Control = (control ?? FormTextField) as ComponentType<Props>;
  // The rest of the props are the control's own, beside the ones given here.
  const props = {
    ...field,
    name: property,
    label,
    validate: fieldValidator(schema, property, options),
  } as unknown as Props;
  return <Control {...props} />;
}
