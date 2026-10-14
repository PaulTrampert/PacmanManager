import type { ComponentType, ReactNode } from "react";
import type { FieldValidator } from "../../api";
import type { FormControlProps } from "./FormControlProps";
import { useFormField } from "./useFormField";

/** What a field inside a `Form` takes, whatever its control. */
export interface FieldInputs<T> {
  /** The field's name, unique within its form. */
  name: string;
  /** The field's label. */
  label: string;
  /** Checks the value in the browser. */
  validate: FieldValidator<T>;
  /** The field's current value. */
  value: T;
  /** Called with the new value as it is edited. */
  onChange: (value: T) => void;
  /** Shown below the field while it has no error to show. */
  helperText?: ReactNode;
  /** The API's message for the field, from the last request's validation `400` or a `409`. */
  serverError?: string;
  /** Disables the field. */
  disabled?: boolean;
}

/** The props of `control` beyond {@link FormControlProps}, which the field passes through to it. */
export type ControlOwnProps<T, Props extends FormControlProps<T>> = NoInfer<
  Omit<Props, keyof FormControlProps<T>>
>;

/** The props {@link ValidatedFormField} takes. */
export type ValidatedFormFieldProps<
  T,
  Props extends FormControlProps<T>,
> = FieldInputs<T> & {
  /** The control that renders the field, a `Form<Type>Field`. */
  control: ComponentType<Props>;
} & ControlOwnProps<T, Props>;

/**
 * A field inside a `Form`, checked by any validator and rendered by `control`. It holds the
 * behaviour every field shares, from `useFormField`, so a control only renders. Forms use
 * `FormField`, which builds the validator from the OpenAPI document's rules; this is what it
 * renders, and it is not exported, so a form never writes a rule of its own.
 */
export function ValidatedFormField<T, Props extends FormControlProps<T>>({
  name,
  validate,
  value,
  serverError,
  control: Control,
  ...rest
}: ValidatedFormFieldProps<T, Props>) {
  const field = useFormField({ name, value, validate, serverError });
  // The rest are the control's own props and the inputs it renders, beside the field's state.
  const props = { ...rest, ...field, name, value } as unknown as Props;
  return <Control {...props} />;
}
