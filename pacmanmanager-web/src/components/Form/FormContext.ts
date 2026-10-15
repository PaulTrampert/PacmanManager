import { createContext, useContext } from "react";

/** What a `Form` tells the fields inside it, and how they report back. */
export interface FormContextValue {
  /** Whether the form has been submitted, after which every field shows its errors. */
  submitted: boolean;
  /**
   * Records whether the field `name` fails the browser's validation. The form does not call
   * `onSubmit` while any field does.
   */
  setFieldFailing: (name: string, failing: boolean) => void;
}

/** The context a `Form` provides to its fields. */
export const FormContext = createContext<FormContextValue | null>(null);

/** The enclosing `Form`'s context. Throws outside a `Form`, where a field could block nothing. */
export function useFormContext(): FormContextValue {
  const context = useContext(FormContext);
  if (context === null) {
    throw new Error("A form field must be rendered inside a Form.");
  }
  return context;
}
