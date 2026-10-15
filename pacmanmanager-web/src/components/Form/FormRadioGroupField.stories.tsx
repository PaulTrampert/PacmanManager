import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { expect, fn, userEvent } from "storybook/test";
import type { FieldValidator } from "../../api";
import { Form } from "./Form";
import {
  FormRadioGroupField,
  type FormRadioGroupOption,
} from "./FormRadioGroupField";
import { ValidatedFormField } from "./ValidatedFormField";

const channels: FormRadioGroupOption<string>[] = [
  { value: "stable", label: "Stable" },
  { value: "testing", label: "Testing" },
  { value: "nightly", label: "Nightly" },
];

const nightlyUnavailable = "Nightly builds are not available yet.";

/** Fails for **Nightly**, so a story can tell whether the field has been left. */
const validateChannel: FieldValidator<string> = (value) =>
  value === "nightly" ? [nightlyUnavailable] : [];

/** The props of {@link ChannelField}. */
interface ChannelFieldProps {
  /** The field's value when the story starts. */
  initialValue?: string;
  helperText?: string;
  serverError?: string;
  disabled?: boolean;
  onChange: (value: string) => void;
}

/** A radio group of a `string`, in a form holding its value. */
function ChannelField({
  initialValue = "",
  onChange,
  ...field
}: ChannelFieldProps) {
  const [channel, setChannel] = useState(initialValue);
  return (
    <Form
      submitLabel="Save"
      pendingLabel="Saving…"
      onSubmit={() => {}}
      onCancel={() => {}}
    >
      <ValidatedFormField
        {...field}
        control={FormRadioGroupField<string>}
        name="channel"
        label="Channel"
        validate={validateChannel}
        value={channel}
        onChange={(value) => {
          setChannel(value);
          onChange(value);
        }}
        options={channels}
      />
    </Form>
  );
}

const meta = {
  component: ChannelField,
  args: { onChange: fn() },
} satisfies Meta<typeof ChannelField>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Pristine: Story = {
  play: async ({ canvas, args }) => {
    const group = canvas.getByRole("radiogroup", { name: "Channel" });
    await expect(group).not.toBeInvalid();
    for (const radio of canvas.getAllByRole("radio")) {
      await expect(radio).not.toBeChecked();
    }

    // Choosing an option reports its value.
    await userEvent.click(canvas.getByRole("radio", { name: "Testing" }));
    await expect(args.onChange).toHaveBeenLastCalledWith("testing");
    await expect(canvas.getByRole("radio", { name: "Testing" })).toBeChecked();
  },
};

export const WithHelperText: Story = {
  args: { helperText: "Which builds the repository takes." },
  play: async ({ canvas }) => {
    await expect(
      canvas.getByRole("radiogroup", { name: "Channel" }),
    ).toHaveAccessibleDescription("Which builds the repository takes.");
  },
};

export const MovingBetweenOptions: Story = {
  args: { initialValue: "stable", helperText: "Which builds it takes." },
  play: async ({ canvas, args }) => {
    const group = canvas.getByRole("radiogroup", { name: "Channel" });

    // Moving onto an option that fails is not leaving the field, so no error shows yet.
    await userEvent.click(canvas.getByRole("radio", { name: "Stable" }));
    await userEvent.keyboard("{ArrowDown}{ArrowDown}");
    await expect(canvas.getByRole("radio", { name: "Nightly" })).toBeChecked();
    await expect(args.onChange).toHaveBeenLastCalledWith("nightly");
    await expect(group).not.toBeInvalid();
    await expect(group).toHaveAccessibleDescription("Which builds it takes.");

    // Leaving the group shows it.
    await userEvent.tab();
    await expect(group).toBeInvalid();
    await expect(group).toHaveAccessibleDescription(nightlyUnavailable);
  },
};

export const ServerError: Story = {
  args: {
    initialValue: "testing",
    helperText: "Which builds it takes.",
    serverError: "The testing channel is full.",
  },
  play: async ({ canvas }) => {
    // The API's error shows at once: the form has already been submitted for it to exist.
    const group = canvas.getByRole("radiogroup", { name: "Channel" });
    await expect(group).toBeInvalid();
    await expect(group).toHaveAccessibleDescription(
      "The testing channel is full.",
    );
    await expect(canvas.getByRole("radio", { name: "Testing" })).toBeChecked();
  },
};

export const Disabled: Story = {
  args: { initialValue: "stable", disabled: true },
  play: async ({ canvas, args }) => {
    for (const radio of canvas.getAllByRole("radio")) {
      await expect(radio).toBeDisabled();
    }
    await expect(canvas.getByRole("radio", { name: "Stable" })).toBeChecked();
    await userEvent.click(canvas.getByRole("radio", { name: "Testing" }), {
      pointerEventsCheck: 0,
    });
    await expect(args.onChange).not.toHaveBeenCalled();
  },
};

const visibilities: FormRadioGroupOption<boolean>[] = [
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

/** The props of {@link VisibilityField}. */
interface VisibilityFieldProps {
  onChange: (value: boolean) => void;
}

/** A radio group of a `boolean` whose options have descriptions, as a repository's visibility. */
function VisibilityField({ onChange }: VisibilityFieldProps) {
  const [isPublic, setIsPublic] = useState(false);
  return (
    <Form
      submitLabel="Create"
      pendingLabel="Creating…"
      onSubmit={() => {}}
      onCancel={() => {}}
    >
      <ValidatedFormField
        control={FormRadioGroupField<boolean>}
        name="isPublic"
        label="Visibility"
        validate={() => []}
        value={isPublic}
        onChange={(value) => {
          setIsPublic(value);
          onChange(value);
        }}
        options={visibilities}
      />
    </Form>
  );
}

export const WithDescriptions: Story = {
  render: (args) => <VisibilityField onChange={args.onChange} />,
  play: async ({ canvas, args }) => {
    const priv = canvas.getByRole("radio", { name: "Private" });
    const pub = canvas.getByRole("radio", { name: "Public" });
    await expect(priv).toBeChecked();
    await expect(priv).toHaveAccessibleDescription(
      "Only you can see it and read its packages.",
    );
    await expect(pub).toHaveAccessibleDescription(
      "Anyone, signed in or not, can see it and read its packages.",
    );

    // The values reported are the options' booleans, not their inputs' strings.
    await userEvent.click(pub);
    await expect(args.onChange).toHaveBeenLastCalledWith(true);
    await userEvent.click(priv);
    await expect(args.onChange).toHaveBeenLastCalledWith(false);
  },
};
