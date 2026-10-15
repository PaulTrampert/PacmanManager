import TextField from "@mui/material/TextField";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { useMutation } from "@tanstack/react-query";
import { HttpResponse } from "msw";
import { useState } from "react";
import { expect, fn, userEvent } from "storybook/test";
import { client, unwrap, type WriteRepositoryRequest } from "../../api";
import { validatorFor } from "../../api/fieldValidator";
import { validationRules } from "../../api/validation.gen";
import { apiHandler } from "../../mocks/api";
import { useFormErrors } from "../../useFormErrors";
import { Form } from "./Form";
import type { FormControlProps } from "./FormControlProps";
import { FormField } from "./FormField";
import { FormTextField } from "./FormTextField";
import { ValidatedFormField } from "./ValidatedFormField";

const helperText =
  "Letters, digits and @ _ + - . This is the section name in pacman.conf.";
const patternMessage =
  "Use letters, digits and @ _ + - .; - and . cannot start or end the name, or appear twice in a row.";

/** The props of {@link NameField}. */
interface NameFieldProps {
  /** The field's value when the story starts. */
  initialValue?: string;
  helperText?: string;
  serverError?: string;
  onSubmit: () => void;
}

/** A repository's name field, in a form holding its value. */
function NameField({
  initialValue = "",
  helperText,
  serverError,
  onSubmit,
}: NameFieldProps) {
  const [name, setName] = useState(initialValue);
  return (
    <Form
      submitLabel="Create"
      pendingLabel="Creating…"
      onSubmit={onSubmit}
      onCancel={() => {}}
    >
      <FormField
        schema="WriteRepositoryRequest"
        property="name"
        label="Name"
        patternMessage={patternMessage}
        value={name}
        onChange={setName}
        helperText={helperText}
        serverError={serverError}
      />
    </Form>
  );
}

const meta = {
  component: NameField,
  args: { onSubmit: fn() },
} satisfies Meta<typeof NameField>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Pristine: Story = {
  play: async ({ canvas }) => {
    const name = canvas.getByRole("textbox", { name: "Name" });
    await expect(name).toHaveValue("");
    await expect(name).not.toBeInvalid();
  },
};

export const WithHelperText: Story = {
  args: { helperText },
  play: async ({ canvas }) => {
    await expect(
      canvas.getByRole("textbox", { name: "Name" }),
    ).toHaveAccessibleDescription(helperText);
  },
};

export const Required: Story = {
  args: { helperText },
  play: async ({ canvas }) => {
    const name = canvas.getByRole("textbox", { name: "Name" });

    // Entering and leaving the empty field is enough to show that it is required.
    await userEvent.click(name);
    await expect(name).not.toBeInvalid();
    await userEvent.tab();

    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription("Name is required.");
    await expect(canvas.queryByText(helperText)).not.toBeInTheDocument();
  },
};

export const TooLong: Story = {
  args: { helperText },
  play: async ({ canvas }) => {
    const { maxLength } = validationRules.WriteRepositoryRequest.name;
    const name = canvas.getByRole("textbox", { name: "Name" });

    await userEvent.click(name);
    await userEvent.paste("a".repeat(maxLength + 1));
    await expect(name).not.toBeInvalid();
    await expect(name).toHaveAccessibleDescription(helperText);

    await userEvent.tab();
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription(
      `Name must be ${maxLength} characters or fewer.`,
    );
  },
};

export const InvalidPattern: Story = {
  args: { helperText },
  play: async ({ canvas }) => {
    const name = canvas.getByRole("textbox", { name: "Name" });

    // No error while the value is first typed, though it fails the pattern from its first key.
    await userEvent.type(name, "-nightly");
    await expect(name).not.toBeInvalid();
    await expect(name).toHaveAccessibleDescription(helperText);

    await userEvent.tab();
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription(patternMessage);

    // Once shown, the error follows the value.
    await userEvent.clear(name);
    await userEvent.type(name, "nightly");
    await expect(name).not.toBeInvalid();
    await expect(name).toHaveAccessibleDescription(helperText);
  },
};

export const FailsTwoRules: Story = {
  args: { helperText },
  play: async ({ canvas }) => {
    const { maxLength } = validationRules.WriteRepositoryRequest.name;
    const tooLong = `Name must be ${maxLength} characters or fewer.`;
    const name = canvas.getByRole("textbox", { name: "Name" });

    // Too long, and starting with a `-` the pattern forbids.
    await userEvent.click(name);
    await userEvent.paste(`-${"a".repeat(maxLength)}`);
    await userEvent.tab();

    // Both messages show, each on a line of its own, in place of the helper text.
    await expect(name).toBeInvalid();
    const first = canvas.getByText(tooLong);
    const second = canvas.getByText(patternMessage);
    await expect(getComputedStyle(first).display).toBe("block");
    await expect(getComputedStyle(second).display).toBe("block");
    await expect(first.parentElement).toBe(second.parentElement);
    await expect(name).toHaveAccessibleDescription(
      expect.stringContaining(tooLong),
    );
    await expect(name).toHaveAccessibleDescription(
      expect.stringContaining(patternMessage),
    );
    await expect(canvas.queryByText(helperText)).not.toBeInTheDocument();
  },
};

/** A field whose rules have a minimum length, which no property in the OpenAPI document has yet. */
function MinLengthField() {
  const [value, setValue] = useState("");
  return (
    <Form
      submitLabel="Save"
      pendingLabel="Saving…"
      onSubmit={() => {}}
      onCancel={() => {}}
    >
      <ValidatedFormField
        control={FormTextField}
        name="code"
        label="Code"
        validate={validatorFor(
          { required: true, minLength: 3 },
          { label: "Code" },
        )}
        value={value}
        onChange={setValue}
      />
    </Form>
  );
}

export const TooShort: Story = {
  render: () => <MinLengthField />,
  play: async ({ canvas }) => {
    const code = canvas.getByRole("textbox", { name: "Code" });

    await userEvent.type(code, "ab");
    await expect(code).not.toBeInvalid();

    await userEvent.tab();
    await expect(code).toBeInvalid();
    await expect(code).toHaveAccessibleDescription(
      "Code must be at least 3 characters.",
    );
  },
};

export const ServerError: Story = {
  args: {
    initialValue: "core",
    helperText,
    serverError: "A repository with this name already exists.",
  },
  play: async ({ canvas }) => {
    // The API's error shows at once: the form has already been submitted for it to exist.
    const name = canvas.getByRole("textbox", { name: "Name" });
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription(
      "A repository with this name already exists.",
    );
  },
};

/** The props of {@link FormNotesField}. */
interface FormNotesFieldProps extends FormControlProps<string> {
  /** How many lines the field shows. */
  rows: number;
}

/** A multi-line text control, standing in for any `Form<Type>Field` with props of its own. */
function FormNotesField({
  onChange,
  errors,
  helperText,
  ...control
}: FormNotesFieldProps) {
  return (
    <TextField
      {...control}
      onChange={(event) => onChange(event.target.value)}
      error={errors.length > 0}
      helperText={errors.length > 0 ? errors.join(" ") : helperText}
      multiline
    />
  );
}

/** The name field rendered by another control. */
function NotesControlField() {
  const [name, setName] = useState("");
  return (
    <Form
      submitLabel="Create"
      pendingLabel="Creating…"
      onSubmit={() => {}}
      onCancel={() => {}}
    >
      <FormField
        schema="WriteRepositoryRequest"
        property="name"
        label="Name"
        patternMessage={patternMessage}
        value={name}
        onChange={setName}
        control={FormNotesField}
        rows={3}
      />
    </Form>
  );
}

export const AnotherControl: Story = {
  render: () => <NotesControlField />,
  play: async ({ canvas }) => {
    // The control renders the field, given its own props, and the schema's rules still apply.
    const name = canvas.getByRole("textbox", { name: "Name" });
    await expect(name.tagName).toBe("TEXTAREA");
    await expect(name).toHaveAttribute("rows", "3");

    await userEvent.click(name);
    await userEvent.tab();
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription("Name is required.");
  },
};

// The message the API answers with for a pattern it alone checks. RepoHost words every
// `[RegularExpression]`'s message for people, so it never quotes the pattern.
const serverPatternMessage =
  "Use letters, digits and @ _ + - .; - and . cannot start or end the name, or appear twice in a row.";

// The repository name's rules with its pattern left to the server, as a pattern the translator
// does not recognise is generated. No property in the OpenAPI document has one yet.
const serverOnlyName = validatorFor(
  {
    ...validationRules.WriteRepositoryRequest.name,
    pattern: { kind: "server-only" },
  },
  { label: "Name", patternMessage },
);

/** A name field whose pattern only the API checks, submitted to the API as a page would. */
function ServerOnlyPatternField() {
  const [name, setName] = useState("");
  const create = useMutation({
    mutationFn: (body: WriteRepositoryRequest) =>
      unwrap(client.POST("/v1/Repositories", { body })),
  });
  const { fieldErrors } = useFormErrors(create.error, ["name"]);
  return (
    <Form
      submitLabel="Create"
      pendingLabel="Creating…"
      pending={create.isPending}
      onSubmit={() => create.mutate({ name, supportedArchitectures: [] })}
      onCancel={() => {}}
    >
      <ValidatedFormField
        control={FormTextField}
        name="name"
        label="Name"
        validate={serverOnlyName}
        value={name}
        onChange={setName}
        helperText={helperText}
        serverError={fieldErrors.name}
      />
    </Form>
  );
}

export const ServerOnlyPattern: Story = {
  render: () => <ServerOnlyPatternField />,
  beforeEach: ({ msw }) => {
    msw.use(
      apiHandler("post", "/v1/Repositories", () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            errors: { Name: [serverPatternMessage] },
          },
          { status: 400 },
        ),
      ),
    );
  },
  play: async ({ canvas }) => {
    const name = canvas.getByRole("textbox", { name: "Name" });

    // The browser lets the value through, since it does not check the pattern…
    await userEvent.type(name, "-nightly");
    await userEvent.tab();
    await expect(name).not.toBeInvalid();
    await userEvent.click(canvas.getByRole("button", { name: "Create" }));

    // …and the API's human message lands on the field, not a regular expression.
    await expect(
      await canvas.findByText(serverPatternMessage),
    ).toBeInTheDocument();
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription(serverPatternMessage);
    await expect(canvas.queryByText(/\\A|\\z|\[a-zA-Z/)).toBeNull();
  },
};
