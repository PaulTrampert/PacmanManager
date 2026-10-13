import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import {
  expect,
  fn,
  screen,
  spyOn,
  userEvent as storyUserEvent,
  within,
} from "storybook/test";
import { FilterSelect, type FilterSelectProps } from "./FilterSelect";

const architectures = [
  { value: "x86_64", label: "64-bit Intel/AMD (x86_64)" },
  { value: "aarch64", label: "64-bit ARM (aarch64)" },
  { value: "i686", label: "32-bit Intel (i686)" },
];

/** A {@link FilterSelect} that keeps the value it reports, as the page's search params will. */
function StatefulFilterSelect(props: FilterSelectProps<string>) {
  const [value, setValue] = useState(props.value);
  return (
    <FilterSelect
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
  component: FilterSelect,
  args: {
    label: "Architecture",
    options: architectures,
    value: undefined,
    onChange: fn(),
    sx: { width: 240 },
  },
  render: (args) => <StatefulFilterSelect {...args} />,
} satisfies Meta<typeof FilterSelect<string>>;

export default meta;

type Story = StoryObj<typeof meta>;

/**
 * Asserts that the filter labelled `name` shows `shown`, cannot be opened, and reports nothing.
 */
async function expectDisabled(
  canvasElement: HTMLElement,
  name: string,
  shown: string,
  onChange: unknown,
) {
  const select = within(canvasElement).getByRole("combobox", { name });
  await expect(select).toHaveAttribute("aria-disabled", "true");
  await expect(select).toHaveTextContent(shown);

  // A disabled field takes no pointer events, which userEvent would otherwise refuse to click
  // through; the point is that the click does nothing.
  const user = storyUserEvent.setup({ pointerEventsCheck: 0 });
  await user.click(select);
  await expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  await expect(onChange).not.toHaveBeenCalled();
}

export const SeveralOptions: Story = {
  play: async ({ args, canvas, userEvent }) => {
    const errors = spyOn(console, "error");
    try {
      const select = canvas.getByRole("combobox", { name: "Architecture" });
      await expect(select).toHaveTextContent("All");
      await expect(select).not.toHaveAttribute("aria-disabled");

      await userEvent.click(select);
      await expect(
        screen.getAllByRole("option").map((option) => option.textContent),
      ).toEqual(["All", ...architectures.map((option) => option.label)]);
      await userEvent.click(
        screen.getByRole("option", { name: "64-bit ARM (aarch64)" }),
      );
      await expect(args.onChange).toHaveBeenLastCalledWith("aarch64");
      await expect(select).toHaveTextContent("64-bit ARM (aarch64)");

      await userEvent.click(select);
      await userEvent.click(screen.getByRole("option", { name: "All" }));
      await expect(args.onChange).toHaveBeenLastCalledWith(undefined);
      await expect(select).toHaveTextContent("All");

      // Back from All to a value: the select stayed controlled throughout.
      await userEvent.click(select);
      await userEvent.click(
        screen.getByRole("option", { name: "32-bit Intel (i686)" }),
      );
      await expect(args.onChange).toHaveBeenLastCalledWith("i686");
      await expect(args.onChange).toHaveBeenCalledTimes(3);
      await expect(errors).not.toHaveBeenCalled();
    } finally {
      errors.mockRestore();
    }
  },
};

export const SingleOption: Story = {
  args: {
    options: architectures.slice(0, 1),
    // A value it is given is not shown: a filter that cannot narrow anything is All.
    value: "x86_64",
  },
  play: async ({ args, canvasElement }) => {
    await expectDisabled(canvasElement, "Architecture", "All", args.onChange);
  },
};

export const NoOptions: Story = {
  args: { options: [] },
  play: async ({ args, canvasElement }) => {
    await expectDisabled(canvasElement, "Architecture", "All", args.onChange);
  },
};

export const DisabledOnValue: Story = {
  args: {
    label: "Visibility",
    options: [
      { value: "public", label: "Public" },
      { value: "private", label: "Private" },
    ],
    disabledValue: "public",
    sx: { width: 130 },
  },
  play: async ({ args, canvasElement }) => {
    await expectDisabled(canvasElement, "Visibility", "Public", args.onChange);
  },
};
