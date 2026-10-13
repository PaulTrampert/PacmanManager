import { createContext, useContext } from "react";

/** What a `Form` tells the fields inside it, and how they report back. */
export interface FormContextValue {
  /** Whether the form has been submitted, after which every field shows its error. */
  submitted: boolean;
  /**
   * Records the browser's validation error for the field `name`, or `undefined` when it passes. The
   * form does not call `onSubmit` while any field has one.
   */
  setFieldError: (name: string, error: string | undefined) => void;
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
