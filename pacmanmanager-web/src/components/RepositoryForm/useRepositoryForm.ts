import { useState } from "react";
import {
  ApiError,
  type Architecture,
  type Repository,
  type WriteRepositoryRequest,
} from "../../api";
import { useFormErrors } from "../../useFormErrors";

/**
 * What `RepositoryForm` submits: the request body, without `supportedArchitectures` when creating,
 * which the API then defaults to every architecture a repository may support.
 */
export type RepositoryFormValues = Omit<
  WriteRepositoryRequest,
  "supportedArchitectures"
> &
  Partial<Pick<WriteRepositoryRequest, "supportedArchitectures">>;

/** The fields of the form, as the request body names them. */
const fields = ["name", "isPublic", "supportedArchitectures"] as const;

/** A field of the form, as the request body names it. */
export type RepositoryFormField = (typeof fields)[number];

/** The message a `409` puts on the name field. */
export const nameTakenMessage = "A repository with this name already exists.";

/** What {@link useRepositoryForm} needs from the form's props. */
export interface UseRepositoryFormOptions {
  /** The repository to edit. Without one, or without an `id`, the form creates a repository. */
  repository?: Repository;
  /** Every architecture a repository may support, from `GET /v1/Architectures`. */
  architectures: readonly Architecture[];
  /** The failed request's error, if the last one failed. */
  error?: unknown;
  /** Called with the form's values when it is submitted valid. */
  onSubmit: (values: RepositoryFormValues) => void;
}

/** What {@link useRepositoryForm} gives the form to render. */
export interface RepositoryFormState {
  /** Whether the form edits an existing repository, rather than creating one. */
  editing: boolean;
  /** The name as edited. */
  name: string;
  /** Sets the name. */
  onNameChange: (name: string) => void;
  /** The visibility as edited. */
  isPublic: boolean;
  /** Sets the visibility. */
  onIsPublicChange: (isPublic: boolean) => void;
  /** The architectures shown as selected. */
  supportedArchitectures: string[];
  /** Sets the architectures. */
  onSupportedArchitecturesChange: (supportedArchitectures: string[]) => void;
  /** The API's message for each field, from a `409` or a validation `400`. */
  fieldErrors: Partial<Record<RepositoryFormField, string>>;
  /** The error to show in the form's alert, if any is not shown on a field. */
  alertError: unknown;
  /** The messages of a validation `400` that name none of the fields, for the alert. */
  alertMessages: string[];
  /** Whether submitting is disabled because nothing has changed. */
  submitDisabled: boolean;
  /** Submits the form's values. */
  onSubmit: () => void;
}

/**
 * The behaviour of `RepositoryForm`, which creates a repository unless it is given one with an `id`
 * to edit: its values start empty and private when creating and as stored when editing, and **Save**
 * waits for the name or the visibility to change. A `409` goes on the name field and a validation
 * `400`'s entries on the fields they name; anything else, and any entry naming no field, is left
 * for the alert.
 */
export function useRepositoryForm({
  repository,
  architectures,
  error,
  onSubmit,
}: UseRepositoryFormOptions): RepositoryFormState {
  const stored = repository?.id ? repository : undefined;
  const editing = stored !== undefined;
  const storedName = stored?.name ?? "";
  const storedIsPublic = stored?.isPublic ?? false;

  const [name, setName] = useState(storedName);
  const [isPublic, setIsPublic] = useState(storedIsPublic);
  // Undefined until chosen, so that creating shows every architecture, even ones that arrive later.
  const [chosenArchitectures, setChosenArchitectures] = useState<
    string[] | undefined
  >(undefined);
  const supportedArchitectures =
    chosenArchitectures ??
    (stored
      ? (stored.supportedArchitectures ?? [])
      : architectures.map((architecture) => architecture.value));

  const { fieldErrors, otherErrors } = useFormErrors(error, fields);
  const conflict = error instanceof ApiError && error.status === 409;
  const validation = error instanceof ApiError && error.errors !== undefined;

  let alertError: unknown = undefined;
  if (validation) {
    alertError = otherErrors.length > 0 ? error : undefined;
  } else if (!conflict && error !== null) {
    alertError = error;
  }

  return {
    editing,
    name,
    onNameChange: setName,
    isPublic,
    onIsPublicChange: setIsPublic,
    supportedArchitectures,
    onSupportedArchitecturesChange: setChosenArchitectures,
    fieldErrors: conflict ? { name: nameTakenMessage } : fieldErrors,
    alertError,
    alertMessages: validation ? otherErrors : [],
    submitDisabled:
      editing && name === storedName && isPublic === storedIsPublic,
    onSubmit: () =>
      onSubmit(
        editing
          ? { name, isPublic, supportedArchitectures }
          : { name, isPublic },
      ),
  };
}
