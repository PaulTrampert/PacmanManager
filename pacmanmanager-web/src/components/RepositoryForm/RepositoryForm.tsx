import type { Architecture, Repository } from "../../api";
import {
  Form,
  FormField,
  FormMultiSelectField,
  FormRadioGroupField,
  FormTextField,
  type FormRadioGroupOption,
} from "../Form";
import {
  useRepositoryForm,
  type RepositoryFormValues,
} from "./useRepositoryForm";

/** The props {@link RepositoryForm} takes. */
export interface RepositoryFormProps {
  /**
   * The repository to edit. Without one, or without an `id`, the form creates a repository: its
   * `id` is the only thing that decides which it does.
   */
  repository?: Repository;
  /** Every architecture a repository may support, from `GET /v1/Architectures`. */
  architectures: readonly Architecture[];
  /**
   * Called with the form's values when it is submitted valid. They always carry `supportedArchitectures`:
   * the repository's own when editing, every architecture when creating.
   */
  onSubmit: (values: RepositoryFormValues) => void;
  /** Called when **Cancel** is chosen. */
  onCancel: () => void;
  /** Whether the request is in flight. While it is, the submit button is disabled. */
  pending?: boolean;
  /**
   * The request's error. A `409` shows on the name field and a validation `400`'s entries on the
   * fields they name; anything else shows in an alert above the fields.
   */
  error?: unknown;
}

const nameHelperText =
  "Letters, digits and @ _ + - . This is the section name in pacman.conf.";

const namePatternMessage =
  "Use letters, digits and @ _ + - .; - and . cannot start or end the name, or appear twice in a row.";

const visibilityOptions: readonly FormRadioGroupOption<boolean>[] = [
  {
    value: false,
    label: "Private",
    description: "Only you can see it and read its packages.",
  },
  {
    value: true,
    label: "Public",
    description: "Anyone, signed in or not, can see it and read its packages.",
  },
];

/**
 * The one form for creating and editing a repository: its name, its visibility, and the
 * architectures it publishes, which cannot be chosen yet. It edits `repository` when that has an
 * `id`, and creates one otherwise. It never calls the API; its page sends what it submits.
 */
export function RepositoryForm({
  repository,
  architectures,
  onSubmit,
  onCancel,
  pending = false,
  error,
}: RepositoryFormProps) {
  const form = useRepositoryForm({
    repository,
    architectures,
    error,
    onSubmit,
  });
  return (
    <Form
      submitLabel={form.editing ? "Save" : "Create"}
      pendingLabel={form.editing ? "Saving…" : "Creating…"}
      onSubmit={form.onSubmit}
      onCancel={onCancel}
      pending={pending}
      submitDisabled={form.submitDisabled}
      error={form.alertError}
      errorMessages={form.alertMessages}
    >
      <FormField
        schema="WriteRepositoryRequest"
        property="name"
        label="Name"
        patternMessage={namePatternMessage}
        value={form.name}
        onChange={form.onNameChange}
        control={FormTextField}
        helperText={nameHelperText}
        serverError={form.fieldErrors.name}
      />
      <FormField
        schema="WriteRepositoryRequest"
        property="isPublic"
        label="Visibility"
        value={form.isPublic}
        onChange={form.onIsPublicChange}
        control={FormRadioGroupField<boolean>}
        options={visibilityOptions}
        serverError={form.fieldErrors.isPublic}
      />
      <FormField
        schema="WriteRepositoryRequest"
        property="supportedArchitectures"
        label="Architectures"
        value={form.supportedArchitectures}
        onChange={form.onSupportedArchitecturesChange}
        control={FormMultiSelectField}
        options={architectures.map((architecture) => ({
          value: architecture.value,
          label: architecture.displayValue,
        }))}
        helperText="Every supported architecture is published."
        serverError={form.fieldErrors.supportedArchitectures}
        disabled
      />
    </Form>
  );
}
