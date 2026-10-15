import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { expect, fn, screen, spyOn, userEvent, within } from "storybook/test";
import {
  FormMultiSelectField,
  type FormMultiSelectFieldProps,
} from "./FormMultiSelectField";

const architectures = [
  { value: "x86_64", label: "64-bit Intel/AMD (x86_64)" },
  { value: "aarch64", label: "64-bit ARM (aarch64)" },
  { value: "i686", label: "32-bit Intel (i686)" },
];

const helperText = "Every supported architecture is published.";

/** A {@link FormMultiSelectField} that keeps the value it reports, as its form will. */
function StatefulFormMultiSelectField(props: FormMultiSelectFieldProps) {
  const [value, setValue] = useState(props.value);
  return (
    <FormMultiSelectField
      {...props}
      value={value}
      onChange={(next) => {
        setValue(next);
        props.onChange(next);
      }}
    />
  );
}

const meta = {
  component: FormMultiSelectField,
  args: {
    id: "architectures",
    name: "supportedArchitectures",
    label: "Architectures",
    options: architectures,
    value: [],
    onChange: fn(),
    onBlur: fn(),
    error: undefined,
  },
  render: (args) => <StatefulFormMultiSelectField {...args} />,
} satisfies Meta<typeof FormMultiSelectField>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Clicks the option labelled `name` in the open menu. */
async function toggle(name: string) {
  await userEvent.click(screen.getByRole("option", { name }));
}

export const Pristine: Story = {
  play: async ({ args, canvas, canvasElement }) => {
    const select = canvas.getByRole("combobox", { name: "Architectures" });
    // MUI shows an empty select as a zero-width space, so the value is read from its input.
    await expect(
      canvasElement.querySelector('input[name="supportedArchitectures"]'),
    ).toHaveValue("");
    await expect(select).not.toHaveAttribute("aria-invalid", "true");
    await expect(select).not.toHaveAttribute("aria-disabled");
    await expect(args.onChange).not.toHaveBeenCalled();
  },
};

export const WithHelperText: Story = {
  args: { helperText },
  play: async ({ canvas }) => {
    await expect(
      canvas.getByRole("combobox", { name: "Architectures" }),
    ).toHaveAccessibleDescription(helperText);
  },
};

export const ChoosingAndClearing: Story = {
  play: async ({ args, canvas }) => {
    const errors = spyOn(console, "error");
    try {
      const select = canvas.getByRole("combobox", { name: "Architectures" });
      await userEvent.click(select);
      await expect(
        screen.getAllByRole("option").map((option) => option.textContent),
      ).toEqual(architectures.map((option) => option.label));

      // Chosen out of order, reported in the options' order.
      await toggle("32-bit Intel (i686)");
      await expect(args.onChange).toHaveBeenLastCalledWith(["i686"]);
      await toggle("64-bit Intel/AMD (x86_64)");
      await expect(args.onChange).toHaveBeenLastCalledWith(["x86_64", "i686"]);
      await toggle("64-bit ARM (aarch64)");
      await expect(args.onChange).toHaveBeenLastCalledWith([
        "x86_64",
        "aarch64",
        "i686",
      ]);

      // Clearing one keeps the rest in order, and clearing them all reports none.
      await toggle("64-bit Intel/AMD (x86_64)");
      await expect(args.onChange).toHaveBeenLastCalledWith(["aarch64", "i686"]);
      await toggle("32-bit Intel (i686)");
      await toggle("64-bit ARM (aarch64)");
      await expect(args.onChange).toHaveBeenLastCalledWith([]);
      await expect(args.onChange).toHaveBeenCalledTimes(6);

      // Moving into the menu and back is not leaving the field.
      await userEvent.keyboard("{Escape}");
      await expect(args.onBlur).not.toHaveBeenCalled();
      await userEvent.tab();
      await expect(args.onBlur).toHaveBeenCalledOnce();
      await expect(errors).not.toHaveBeenCalled();
    } finally {
      errors.mockRestore();
    }
  },
};

export const SeveralSelected: Story = {
  args: { value: ["x86_64", "aarch64"], helperText },
  play: async ({ canvas }) => {
    const select = canvas.getByRole("combobox", { name: "Architectures" });
    await expect(select).toHaveTextContent(
      "64-bit Intel/AMD (x86_64), 64-bit ARM (aarch64)",
    );

    await userEvent.click(select);
    const listbox = screen.getByRole("listbox");
    await expect(
      within(listbox)
        .getAllByRole("option", { selected: true })
        .map((option) => option.textContent),
    ).toEqual(["64-bit Intel/AMD (x86_64)", "64-bit ARM (aarch64)"]);
    await userEvent.keyboard("{Escape}");
  },
};

export const ValueNotAmongOptions: Story = {
  args: { value: ["x86_64", "riscv64"], helperText },
  play: async ({ args, canvas }) => {
    // A value the options do not have is shown as it is, after the labelled ones.
    const select = canvas.getByRole("combobox", { name: "Architectures" });
    await expect(select).toHaveTextContent(
      "64-bit Intel/AMD (x86_64), riscv64",
    );

    // Choosing an option keeps it, and still reports the options' values in their order.
    await userEvent.click(select);
    await toggle("32-bit Intel (i686)");
    await expect(args.onChange).toHaveBeenLastCalledWith([
      "x86_64",
      "i686",
      "riscv64",
    ]);
    await userEvent.keyboard("{Escape}");
  },
};

export const ServerError: Story = {
  args: {
    value: ["x86_64"],
    helperText,
    error: "Supported architectures must not be empty.",
  },
  play: async ({ canvas }) => {
    const select = canvas.getByRole("combobox", { name: "Architectures" });
    await expect(select).toHaveAttribute("aria-invalid", "true");
    await expect(select).toHaveAccessibleDescription(
      "Supported architectures must not be empty.",
    );
    await expect(canvas.queryByText(helperText)).not.toBeInTheDocument();
  },
};

export const Disabled: Story = {
  args: { value: ["x86_64"], helperText, disabled: true },
  play: async ({ args, canvas }) => {
    const select = canvas.getByRole("combobox", { name: "Architectures" });
    await expect(select).toHaveAttribute("aria-disabled", "true");
    await expect(select).toHaveTextContent("64-bit Intel/AMD (x86_64)");

    // A disabled field takes no pointer events, which userEvent would otherwise refuse to click
    // through; the point is that the click does nothing.
    const user = userEvent.setup({ pointerEventsCheck: 0 });
    await user.click(select);
    await expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    await expect(args.onChange).not.toHaveBeenCalled();
  },
};
