import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, screen, userEvent, within } from "storybook/test";
import { aRepository } from "../../mocks/fixtures";
import { DeleteRepositoryDialog } from "./DeleteRepositoryDialog";

const meta = {
  component: DeleteRepositoryDialog,
  args: {
    open: true,
    repository: aRepository({ name: "core-extras" }),
    onConfirm: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof DeleteRepositoryDialog>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The dialog, which MUI renders in a portal outside the story's canvas. */
async function findDialog(): Promise<HTMLElement> {
  return screen.findByRole("dialog", { name: "Delete core-extras?" });
}

export const Open: Story = {
  play: async ({ args }) => {
    const dialog = await findDialog();
    await expect(dialog).toHaveTextContent(
      "This permanently deletes core-extras and every package in it. Clients using it will fail. This can't be undone.",
    );
    const field = within(dialog).getByRole("textbox", {
      name: "Type core-extras to confirm",
    });
    await expect(field).toHaveFocus();
    await expect(field).toHaveValue("");

    const remove = within(dialog).getByRole("button", { name: "Delete" });
    await expect(remove).toBeDisabled();

    // Anything but the exact name, case included, leaves Delete disabled.
    for (const attempt of [
      "core-extra",
      "Core-Extras",
      "CORE-EXTRAS",
      "core-extras ",
    ]) {
      await userEvent.clear(field);
      await userEvent.type(field, attempt);
      await expect(remove).toBeDisabled();
    }

    await userEvent.click(
      within(dialog).getByRole("button", { name: "Cancel" }),
    );
    await expect(args.onCancel).toHaveBeenCalledOnce();
    await expect(args.onConfirm).not.toHaveBeenCalled();
  },
};

export const NameTyped: Story = {
  play: async ({ args }) => {
    const dialog = await findDialog();
    const field = within(dialog).getByRole("textbox", {
      name: "Type core-extras to confirm",
    });
    const remove = within(dialog).getByRole("button", { name: "Delete" });

    await userEvent.type(field, "core-extras");
    await expect(remove).toBeEnabled();

    // Changing a letter's case disables it again.
    await userEvent.clear(field);
    await userEvent.type(field, "core-Extras");
    await expect(remove).toBeDisabled();

    await userEvent.clear(field);
    await userEvent.type(field, "core-extras");
    await expect(remove).toBeEnabled();

    await userEvent.click(remove);
    await expect(args.onConfirm).toHaveBeenCalledOnce();
    await expect(args.onCancel).not.toHaveBeenCalled();
  },
};

export const Pending: Story = {
  args: { pending: true },
  play: async ({ args }) => {
    const dialog = await findDialog();
    const field = within(dialog).getByRole("textbox", {
      name: "Type core-extras to confirm",
    });
    const remove = within(dialog).getByRole("button", { name: "Delete" });

    // Even the exact name leaves Delete disabled while the request is in flight.
    await userEvent.type(field, "core-extras");
    await expect(remove).toBeDisabled();
    await expect(
      within(dialog).getByRole("button", { name: "Cancel" }),
    ).toBeDisabled();

    await userEvent.keyboard("{Escape}");
    await expect(args.onCancel).not.toHaveBeenCalled();
    await expect(args.onConfirm).not.toHaveBeenCalled();
  },
};
