import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { expect, fn, userEvent } from "storybook/test";
import { ApiError } from "../../api";
import { Form } from "./Form";
import { FormField } from "./FormField";
import { FormTextField } from "./FormTextField";

const patternMessage =
  "Use letters, digits and @ _ + - .; - and . cannot start or end the name, or appear twice in a row.";

/** Two fields holding their own values, so a story can type into them. */
function Fields() {
  const [name, setName] = useState("");
  const [displayName, setDisplayName] = useState("");
  return (
    <>
      <FormField
        schema="WriteRepositoryRequest"
        property="name"
        label="Name"
        patternMessage={patternMessage}
        value={name}
        onChange={setName}
        control={FormTextField}
        helperText="Letters, digits and @ _ + - . This is the section name in pacman.conf."
      />
      <FormField
        schema="WriteUserRequest"
        property="displayName"
        label="Display name"
        value={displayName}
        onChange={setDisplayName}
        control={FormTextField}
      />
    </>
  );
}

const meta = {
  component: Form,
  args: {
    children: <Fields />,
    submitLabel: "Create",
    pendingLabel: "Creating…",
    onSubmit: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof Form>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {
  play: async ({ args, canvas }) => {
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();
    await expect(
      canvas.getByRole("textbox", { name: "Name" }),
    ).not.toBeInvalid();
    await expect(canvas.getByRole("button", { name: "Create" })).toBeEnabled();

    await userEvent.click(canvas.getByRole("button", { name: "Cancel" }));
    await expect(args.onCancel).toHaveBeenCalledOnce();
    await expect(args.onSubmit).not.toHaveBeenCalled();
  },
};

export const SubmitInvalid: Story = {
  play: async ({ args, canvas }) => {
    const name = canvas.getByRole("textbox", { name: "Name" });
    const displayName = canvas.getByRole("textbox", { name: "Display name" });

    // Submitting shows every field's error, though neither field was ever left, and sends nothing.
    await userEvent.click(canvas.getByRole("button", { name: "Create" }));
    await expect(name).toHaveAccessibleDescription("Name is required.");
    await expect(displayName).toHaveAccessibleDescription(
      "Display name is required.",
    );
    await expect(args.onSubmit).not.toHaveBeenCalled();

    // One field fixed is not enough.
    await userEvent.type(name, "-nightly");
    await expect(name).toHaveAccessibleDescription(patternMessage);
    await userEvent.clear(name);
    await userEvent.type(name, "nightly-builds");
    await userEvent.click(canvas.getByRole("button", { name: "Create" }));
    await expect(args.onSubmit).not.toHaveBeenCalled();

    await userEvent.type(displayName, "Paul");
    await userEvent.click(canvas.getByRole("button", { name: "Create" }));
    await expect(name).not.toBeInvalid();
    await expect(displayName).not.toBeInvalid();
    await expect(args.onSubmit).toHaveBeenCalledOnce();
  },
};

export const Pending: Story = {
  args: { pending: true },
  play: async ({ args, canvas }) => {
    await expect(
      canvas.getByRole("button", { name: "Creating…" }),
    ).toBeDisabled();
    await expect(
      canvas.queryByRole("button", { name: "Create" }),
    ).not.toBeInTheDocument();

    // Pressing Enter in a field doesn't submit the form again either.
    await userEvent.type(
      canvas.getByRole("textbox", { name: "Name" }),
      "a{Enter}",
    );
    await expect(args.onSubmit).not.toHaveBeenCalled();

    // Cancel stays available while the request is in flight.
    const cancel = canvas.getByRole("button", { name: "Cancel" });
    await expect(cancel).toBeEnabled();
    await userEvent.click(cancel);
    await expect(args.onCancel).toHaveBeenCalledOnce();
  },
};

export const SubmitDisabled: Story = {
  args: { submitLabel: "Save", pendingLabel: "Saving…", submitDisabled: true },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("button", { name: "Save" })).toBeDisabled();
    await expect(canvas.getByRole("button", { name: "Cancel" })).toBeEnabled();
  },
};

export const WithError: Story = {
  args: {
    error: new ApiError(500, { title: "An error occurred on the server." }),
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("alert")).toHaveTextContent(
      "An error occurred on the server.",
    );
  },
};

export const WithUnmatchedValidationErrors: Story = {
  args: {
    error: new ApiError(400, {
      title: "One or more validation errors occurred.",
      errors: { Upload: ["The file is not a pacman package."] },
    }),
    errorMessages: ["The file is not a pacman package."],
  },
  play: async ({ canvas }) => {
    const alert = canvas.getByRole("alert");
    await expect(alert).toHaveTextContent(
      "One or more validation errors occurred.",
    );
    await expect(alert).toHaveTextContent("The file is not a pacman package.");
  },
};
