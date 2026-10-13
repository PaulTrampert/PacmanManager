import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, screen, waitFor, within } from "storybook/test";
import { AsyncAutocomplete, searchDelayMs } from "./AsyncAutocomplete";

interface Person {
  id: string;
  name: string;
}

const me: Person = { id: "1", name: "Paul Trampert" };
const alex: Person = { id: "2", name: "Alex Morgan" };
const sam: Person = { id: "3", name: "Sam Lee" };

const meta = {
  component: AsyncAutocomplete<Person>,
  args: {
    label: "Owner",
    value: undefined,
    onChange: fn(),
    onSearch: fn(),
    // The search returns the pinned user too; the component must list them only once.
    options: [me, alex, sam],
    pinnedOptions: [me],
    getOptionKey: (person) => person.id,
    getOptionLabel: (person) =>
      person.id === me.id ? `${person.name} (you)` : person.name,
    getOptionSearchText: (person) => person.name,
  },
} satisfies Meta<typeof AsyncAutocomplete<Person>>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Waits long enough for a search the component scheduled to have been made. */
const pause = () =>
  new Promise((resolve) => setTimeout(resolve, searchDelayMs + 200));

export const Closed: Story = {
  play: async ({ args, canvas }) => {
    const field = canvas.getByRole("combobox", { name: "Owner" });
    await expect(field).toBeEnabled();
    await expect(field).toHaveValue("");
    await expect(field).toHaveAttribute("placeholder", "All");
    await expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    await expect(args.onSearch).not.toHaveBeenCalled();
  },
};

export const Chosen: Story = {
  args: { value: me },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("combobox", { name: "Owner" })).toHaveValue(
      "Paul Trampert (you)",
    );
  },
};

export const OpenWithNoText: Story = {
  play: async ({ args, canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("combobox", { name: "Owner" }));

    const listbox = await screen.findByRole("listbox");
    // The pinned user comes first, once, then a divider, then the rest of the results.
    await expect(
      within(listbox)
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["Paul Trampert (you)", "Alex Morgan", "Sam Lee"]);
    const items = Array.from(listbox.children);
    await expect(items[1]).toHaveAttribute("role", "separator");

    await userEvent.click(
      within(listbox).getByRole("option", { name: "Alex Morgan" }),
    );
    await expect(args.onChange).toHaveBeenLastCalledWith(alex);

    // Choosing an option is not a search.
    await pause();
    await expect(args.onSearch).not.toHaveBeenCalled();
  },
};

export const OpenWithText: Story = {
  args: { options: [alex] },
  play: async ({ args, canvas, userEvent }) => {
    const field = canvas.getByRole("combobox", { name: "Owner" });
    await userEvent.type(field, "al");

    // Typing alone searches nothing; the search comes once typing stops, for the whole text.
    await expect(args.onSearch).not.toHaveBeenCalled();
    await waitFor(() => expect(args.onSearch).toHaveBeenCalledOnce());
    await expect(args.onSearch).toHaveBeenLastCalledWith("al");

    await userEvent.type(field, "ex");
    await pause();
    await expect(args.onSearch).toHaveBeenCalledTimes(2);
    await expect(args.onSearch).toHaveBeenLastCalledWith("alex");

    // The pinned user does not match, so only the results are listed, with no divider.
    const listbox = await screen.findByRole("listbox");
    await expect(
      within(listbox)
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["Alex Morgan"]);
    await expect(
      within(listbox).queryByRole("separator"),
    ).not.toBeInTheDocument();

    // A pinned option matches on its search text, not on its label's "(you)".
    await userEvent.clear(field);
    await userEvent.type(field, "TRAMP");
    await expect(
      await screen.findByRole("option", { name: "Paul Trampert (you)" }),
    ).toBeVisible();
  },
};

export const Loading: Story = {
  args: { options: [], pinnedOptions: [], loading: true },
  play: async ({ canvas, userEvent }) => {
    await expect(canvas.getByLabelText("Searching")).toBeVisible();
    await userEvent.click(canvas.getByRole("combobox", { name: "Owner" }));
    await expect(await screen.findByText("Loading…")).toBeVisible();
  },
};

export const Disabled: Story = {
  args: { totalOptions: 1, value: alex },
  play: async ({ canvas }) => {
    const field = canvas.getByRole("combobox", { name: "Owner" });
    await expect(field).toBeDisabled();
    // Disabled, it reads **All** whatever it was given, and offers nothing to open or clear.
    await expect(field).toHaveValue("");
    await expect(field).toHaveAttribute("placeholder", "All");
    await expect(canvas.getByRole("button", { name: "Open" })).toBeDisabled();
    await expect(
      canvas.queryByRole("button", { name: "Clear" }),
    ).not.toBeInTheDocument();
  },
};

export const Cleared: Story = {
  args: { value: alex },
  play: async ({ args, canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("combobox", { name: "Owner" }));
    await userEvent.click(canvas.getByRole("button", { name: "Clear" }));
    await expect(args.onChange).toHaveBeenLastCalledWith(undefined);
  },
};
