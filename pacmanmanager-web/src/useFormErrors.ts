import { ApiError } from "./api";

/** A form's validation errors, as {@link useFormErrors} sorts them. */
export interface FormErrors<Field extends string> {
  /** The messages for each field that has any, joined into one string to show beside the field. */
  fieldErrors: Partial<Record<Field, string>>;
  /** The messages for entries that name none of the form's fields, to show above the form. */
  otherErrors: string[];
}

function matchField<Field extends string>(
  key: string,
  fields: readonly Field[],
): Field | undefined {
  // ASP.NET Core names an entry after the C# property (`Name`), and may prefix it with the
  // parameter or a JSON path (`request.Name`, `$.name`); a form names its fields as the JSON does.
  const name = key.slice(key.lastIndexOf(".") + 1).toLowerCase();
  return fields.find((field) => field.toLowerCase() === name);
}

/**
 * Maps the `errors` of a validation response onto a form's fields: each entry that names one of
 * `fields`, ignoring case and any prefix, goes to that field, and the rest are left for a
 * `ProblemDetailsAlert` above the form. An error that is not a validation response maps to nothing.
 *
 * @param error the form's mutation error, if any.
 * @param fields the names of the form's fields, as the request body names them.
 *
 * @example
 * const { fieldErrors, otherErrors } = useFormErrors(mutation.error, ["name", "description"]);
 * <TextField error={!!fieldErrors.name} helperText={fieldErrors.name} ... />
 */
export function useFormErrors<Field extends string>(
  error: unknown,
  fields: readonly Field[],
): FormErrors<Field> {
  // Cheap enough to work out on every render, which spares callers keeping `fields` stable.
  return mapFormErrors(error, fields);
}

/** What {@link useFormErrors} computes, outside React. */
export function mapFormErrors<Field extends string>(
  error: unknown,
  fields: readonly Field[],
): FormErrors<Field> {
  const fieldMessages: Partial<Record<Field, string[]>> = {};
  const otherErrors: string[] = [];
  const errors = error instanceof ApiError ? error.errors : undefined;
  for (const [key, messages] of Object.entries(errors ?? {})) {
    const field = matchField(key, fields);
    if (field === undefined) {
      otherErrors.push(...messages);
    } else {
      (fieldMessages[field] ??= []).push(...messages);
    }
  }
  const fieldErrors: Partial<Record<Field, string>> = {};
  for (const field of fields) {
    const messages = fieldMessages[field];
    if (messages?.length) {
      fieldErrors[field] = messages.join(" ");
    }
  }
  return { fieldErrors, otherErrors };
}
