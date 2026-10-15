import type { ComponentType } from "react";
import {
  fieldValidator,
  type FieldValidatorOptions,
  type PropertyValue,
  type ValidatedProperty,
  type ValidatedSchema,
} from "../../api";
import type { FormControlProps } from "./FormControlProps";
import { FormTextField } from "./FormTextField";
import {
  ValidatedFormField,
  type ControlOwnProps,
  type FieldInputs,
  type ValidatedFormFieldProps,
} from "./ValidatedFormField";

/**
 * The `control` prop of a field holding a `Value`: optional for a `string`, which
 * {@link FormTextField} renders when it is left out, and required for any other type.
 */
type ControlProp<Value, Props> = [Value] extends [string]
  ? {
      /**
       * The control that renders the field, a `Form<Type>Field` of the property's type;
       * {@link FormTextField} when left out. Any props it takes beyond {@link FormControlProps} are
       * passed to it.
       */
      control?: ComponentType<Props>;
    }
  : {
      /**
       * The control that renders the field, a `Form<Type>Field` of the property's type. Any props it
       * takes beyond {@link FormControlProps} are passed to it.
       */
      control: ComponentType<Props>;
    };

/** The props {@link FormField} takes. */
export type FormFieldProps<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
  Props extends FormControlProps<PropertyValue<Schema, Property>> =
    FormControlProps<PropertyValue<Schema, Property>>,
> = {
  /** The request-body schema the field's value is sent in, as the OpenAPI document names it. */
  schema: Schema;
  /** The property of `schema` the field edits, which also names the field. */
  property: Property;
} & ControlProp<PropertyValue<Schema, Property>, Props> &
  Omit<
    FieldInputs<PropertyValue<Schema, Property>>,
    "name" | "label" | "validate"
  > &
  FieldValidatorOptions<Schema, Property> &
  // Props is inferred from `control` alone, and is FormTextFieldProps without one.
  ControlOwnProps<PropertyValue<Schema, Property>, Props>;

/**
 * A field inside a `Form`, validated by the rules the OpenAPI document gives `property` of `schema`
 * and rendered by `control`, a control of the property's type; an outlined text field by default
 * for a `string`. Its errors replace its helper text once the field has been left or the form
 * submitted, and a failing field stops the form submitting. A form names the schema, the property
 * and the label, and words only a pattern's message; it never states a length or a pattern itself.
 */
export function FormField<
  Schema extends ValidatedSchema,
  Property extends ValidatedProperty<Schema>,
  Props extends FormControlProps<PropertyValue<Schema, Property>> =
    FormControlProps<PropertyValue<Schema, Property>>,
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
  // `control` is left out only for a `string` property, whose Props is then FormTextFieldProps.
  const Control = (control ?? FormTextField) as ComponentType<Props>;
  // The rest are the field's inputs and the control's own props, which TypeScript cannot follow
  // through a spread of a generic type.
  const props = {
    ...field,
    name: property,
    label,
    validate: fieldValidator(schema, property, options),
    control: Control,
  } as unknown as ValidatedFormFieldProps<
    PropertyValue<Schema, Property>,
    Props
  >;
  return <ValidatedFormField {...props} />;
}
