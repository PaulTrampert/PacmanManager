import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent, type within } from "storybook/test";
import { ApiError } from "../../api";
import { aRepository, anArchitecture } from "../../mocks/fixtures";
import { RepositoryForm } from "./RepositoryForm";

type Canvas = ReturnType<typeof within>;

const nameHelperText =
  "Letters, digits and @ _ + - . This is the section name in pacman.conf.";
const patternMessage =
  "Use letters, digits and @ _ + - .; - and . cannot start or end the name, or appear twice in a row.";
const nameTaken = "A repository with this name already exists.";

const x86_64 = anArchitecture({
  value: "x86_64",
  displayValue: "64-bit Intel/AMD (x86_64)",
});
const aarch64 = anArchitecture({
  value: "aarch64",
  displayValue: "64-bit ARM (aarch64)",
});

const coreExtras = aRepository({
  id: "a0000000-0000-4000-8000-000000000001",
  name: "core-extras",
  isPublic: true,
  supportedArchitectures: ["x86_64"],
});

const conflict = new ApiError(409, {
  title: "Conflict",
  status: 409,
  detail: "A repository named games already exists.",
});

const meta = {
  component: RepositoryForm,
  args: {
    architectures: [x86_64],
    onSubmit: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof RepositoryForm>;

export default meta;

type Story = StoryObj<typeof meta>;

function nameField(canvas: Canvas) {
  return canvas.getByRole("textbox", { name: "Name" });
}

function architecturesField(canvas: Canvas) {
  return canvas.getByRole("combobox", { name: "Architectures" });
}

export const NewEmpty: Story = {
  play: async ({ args, canvas }) => {
    const name = nameField(canvas);
    await expect(name).toHaveValue("");
    await expect(name).not.toBeInvalid();
    await expect(name).toHaveAccessibleDescription(nameHelperText);

    await expect(
      canvas.getByRole("radiogroup", { name: "Visibility" }),
    ).toBeInTheDocument();
    await expect(canvas.getByRole("radio", { name: "Private" })).toBeChecked();
    await expect(
      canvas.getByRole("radio", { name: "Public" }),
    ).not.toBeChecked();
    await expect(
      canvas.getByText("Only you can see it and read its packages."),
    ).toBeVisible();
    await expect(
      canvas.getByText(
        "Anyone, signed in or not, can see it and read its packages.",
      ),
    ).toBeVisible();

    const architectures = architecturesField(canvas);
    await expect(architectures).toHaveAttribute("aria-disabled", "true");
    await expect(architectures).toHaveTextContent("64-bit Intel/AMD (x86_64)");
    await expect(architectures).toHaveAccessibleDescription(
      "Every supported architecture is published.",
    );

    await expect(canvas.getByRole("button", { name: "Create" })).toBeEnabled();
    await expect(
      canvas.queryByRole("button", { name: "Save" }),
    ).not.toBeInTheDocument();
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();

    await userEvent.click(canvas.getByRole("button", { name: "Cancel" }));
    await expect(args.onCancel).toHaveBeenCalledOnce();
    await expect(args.onSubmit).not.toHaveBeenCalled();
  },
};

export const NewFilledIn: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.type(nameField(canvas), "nightly-builds");
    await userEvent.click(canvas.getByRole("radio", { name: "Public" }));
    await expect(nameField(canvas)).not.toBeInvalid();

    await userEvent.click(canvas.getByRole("button", { name: "Create" }));
    // Creating sends no architectures, so the API publishes every one.
    await expect(args.onSubmit).toHaveBeenCalledOnce();
    await expect(args.onSubmit).toHaveBeenCalledWith({
      name: "nightly-builds",
      isPublic: true,
    });
  },
};

export const NewInvalidName: Story = {
  play: async ({ args, canvas }) => {
    const name = nameField(canvas);
    await userEvent.type(name, "-nightly");
    // Nothing complains while the name is first typed.
    await expect(name).not.toBeInvalid();

    await userEvent.tab();
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription(patternMessage);

    await userEvent.click(canvas.getByRole("button", { name: "Create" }));
    await expect(args.onSubmit).not.toHaveBeenCalled();
  },
};

export const NewNameTaken: Story = {
  args: { error: conflict },
  play: async ({ canvas }) => {
    const name = nameField(canvas);
    await userEvent.type(name, "games");
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription(nameTaken);
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();
  },
};

export const NewRequired: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Create" }));
    const name = nameField(canvas);
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription("Name is required.");
    await expect(args.onSubmit).not.toHaveBeenCalled();
  },
};

export const NewSubmitting: Story = {
  args: { pending: true },
  play: async ({ args, canvas }) => {
    await expect(
      canvas.getByRole("button", { name: "Creating…" }),
    ).toBeDisabled();
    await userEvent.type(nameField(canvas), "nightly-builds{Enter}");
    await expect(args.onSubmit).not.toHaveBeenCalled();
  },
};

export const NewServerError: Story = {
  args: {
    error: new ApiError(500, { title: "An error occurred on the server." }),
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("alert")).toHaveTextContent(
      "An error occurred on the server.",
    );
    await expect(nameField(canvas)).not.toBeInvalid();
  },
};

export const NewValidationError: Story = {
  args: {
    error: new ApiError(400, {
      title: "One or more validation errors occurred.",
      errors: {
        Name: ["The name is reserved."],
        Owner: ["The owner may create no more repositories."],
      },
    }),
  },
  play: async ({ canvas }) => {
    await expect(nameField(canvas)).toHaveAccessibleDescription(
      "The name is reserved.",
    );
    const alert = canvas.getByRole("alert");
    await expect(alert).toHaveTextContent(
      "The owner may create no more repositories.",
    );
    await expect(alert).not.toHaveTextContent("The name is reserved.");
  },
};

export const EditUnchanged: Story = {
  args: { repository: coreExtras },
  play: async ({ args, canvas }) => {
    await expect(nameField(canvas)).toHaveValue("core-extras");
    await expect(canvas.getByRole("radio", { name: "Public" })).toBeChecked();
    await expect(architecturesField(canvas)).toHaveTextContent(
      "64-bit Intel/AMD (x86_64)",
    );
    await expect(
      canvas.queryByRole("button", { name: "Create" }),
    ).not.toBeInTheDocument();

    // Save waits for a change, and changing a value back disables it again.
    const save = canvas.getByRole("button", { name: "Save" });
    await expect(save).toBeDisabled();
    await userEvent.type(nameField(canvas), "-2");
    await expect(save).toBeEnabled();
    await userEvent.type(nameField(canvas), "{Backspace}{Backspace}");
    await expect(save).toBeDisabled();
    await userEvent.click(canvas.getByRole("radio", { name: "Private" }));
    await expect(save).toBeEnabled();
    await userEvent.click(canvas.getByRole("radio", { name: "Public" }));
    await expect(save).toBeDisabled();

    await userEvent.click(canvas.getByRole("button", { name: "Cancel" }));
    await expect(args.onCancel).toHaveBeenCalledOnce();
    await expect(args.onSubmit).not.toHaveBeenCalled();
  },
};

export const EditChanged: Story = {
  args: { repository: coreExtras },
  play: async ({ args, canvas }) => {
    const name = nameField(canvas);
    await userEvent.clear(name);
    await userEvent.type(name, "games");
    await userEvent.click(canvas.getByRole("radio", { name: "Private" }));

    await userEvent.click(canvas.getByRole("button", { name: "Save" }));
    // The repository's architectures are sent back unchanged.
    await expect(args.onSubmit).toHaveBeenCalledOnce();
    await expect(args.onSubmit).toHaveBeenCalledWith({
      name: "games",
      isPublic: false,
      supportedArchitectures: ["x86_64"],
    });
  },
};

export const EditSaving: Story = {
  args: { repository: coreExtras, pending: true },
  play: async ({ canvas }) => {
    await expect(
      canvas.getByRole("button", { name: "Saving…" }),
    ).toBeDisabled();
  },
};

export const EditNameTaken: Story = {
  args: { repository: coreExtras, error: conflict },
  play: async ({ canvas }) => {
    const name = nameField(canvas);
    await userEvent.clear(name);
    await userEvent.type(name, "games");
    await expect(name).toBeInvalid();
    await expect(name).toHaveAccessibleDescription(nameTaken);
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();
    await expect(canvas.getByRole("button", { name: "Save" })).toBeEnabled();
  },
};

export const EditTwoArchitectures: Story = {
  args: {
    architectures: [x86_64, aarch64],
    repository: aRepository({
      ...coreExtras,
      supportedArchitectures: ["x86_64", "aarch64"],
    }),
  },
  play: async ({ args, canvas }) => {
    await expect(architecturesField(canvas)).toHaveTextContent(
      "64-bit Intel/AMD (x86_64), 64-bit ARM (aarch64)",
    );

    await userEvent.click(canvas.getByRole("radio", { name: "Private" }));
    await userEvent.click(canvas.getByRole("button", { name: "Save" }));
    await expect(args.onSubmit).toHaveBeenCalledOnce();
    await expect(args.onSubmit).toHaveBeenCalledWith({
      name: "core-extras",
      isPublic: false,
      supportedArchitectures: ["x86_64", "aarch64"],
    });
  },
};
