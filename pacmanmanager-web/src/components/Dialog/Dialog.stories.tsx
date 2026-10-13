import DialogContentText from "@mui/material/DialogContentText";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, screen, userEvent, within } from "storybook/test";
import { Dialog } from "./Dialog";

const meta = {
  component: Dialog,
  args: {
    open: true,
    title: "Save these changes?",
    children: (
      <DialogContentText>
        Clients already using core-extras will be affected.
      </DialogContentText>
    ),
    confirmLabel: "Save",
    onConfirm: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof Dialog>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The dialog's backdrop, which MUI renders in a portal outside the story's canvas. */
function backdrop(): HTMLElement {
  const element = document.querySelector<HTMLElement>(".MuiBackdrop-root");
  if (!element) {
    throw new Error("The dialog has no backdrop.");
  }
  return element;
}

export const Plain: Story = {
  play: async ({ args }) => {
    // The dialog is in a portal, so it is found on the screen rather than in the canvas.
    const dialog = await screen.findByRole("dialog", {
      name: "Save these changes?",
    });
    await expect(dialog).toHaveTextContent(
      "Clients already using core-extras will be affected.",
    );
    const cancel = within(dialog).getByRole("button", { name: "Cancel" });
    const save = within(dialog).getByRole("button", { name: "Save" });
    await expect(cancel).toBeEnabled();
    await expect(save).toBeEnabled();

    await userEvent.click(save);
    await expect(args.onConfirm).toHaveBeenCalledOnce();
    await expect(args.onCancel).not.toHaveBeenCalled();

    await userEvent.click(cancel);
    await expect(args.onCancel).toHaveBeenCalledTimes(1);

    await userEvent.keyboard("{Escape}");
    await expect(args.onCancel).toHaveBeenCalledTimes(2);

    await userEvent.click(backdrop());
    await expect(args.onCancel).toHaveBeenCalledTimes(3);
    await expect(args.onConfirm).toHaveBeenCalledOnce();
  },
};

export const ConfirmDisabled: Story = {
  args: {
    title: "Delete core-extras?",
    children: (
      <DialogContentText>
        This permanently deletes core-extras and every package in it.
      </DialogContentText>
    ),
    confirmLabel: "Delete",
    confirmColor: "error",
    confirmDisabled: true,
  },
  play: async ({ args }) => {
    const dialog = await screen.findByRole("dialog", {
      name: "Delete core-extras?",
    });
    // Only the confirming action is disabled; the dialog can still be cancelled.
    await expect(
      within(dialog).getByRole("button", { name: "Delete" }),
    ).toBeDisabled();
    const cancel = within(dialog).getByRole("button", { name: "Cancel" });
    await expect(cancel).toBeEnabled();

    await userEvent.click(cancel);
    await expect(args.onCancel).toHaveBeenCalledOnce();
    await expect(args.onConfirm).not.toHaveBeenCalled();
  },
};

export const Pending: Story = {
  args: { pending: true },
  play: async ({ args }) => {
    const dialog = await screen.findByRole("dialog", {
      name: "Save these changes?",
    });
    await expect(
      within(dialog).getByRole("button", { name: "Cancel" }),
    ).toBeDisabled();
    await expect(
      within(dialog).getByRole("button", { name: "Save" }),
    ).toBeDisabled();

    // Neither Escape nor the backdrop dismisses a dialog whose request is in flight.
    await userEvent.keyboard("{Escape}");
    await userEvent.click(backdrop());

    await expect(args.onCancel).not.toHaveBeenCalled();
    await expect(args.onConfirm).not.toHaveBeenCalled();
    await expect(dialog).toBeVisible();
  },
};
