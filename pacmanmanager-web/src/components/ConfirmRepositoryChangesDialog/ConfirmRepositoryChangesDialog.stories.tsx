import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, screen, userEvent, within } from "storybook/test";
import { aRepository } from "../../mocks/fixtures";
import { ConfirmRepositoryChangesDialog } from "./ConfirmRepositoryChangesDialog";

const publicRepository = aRepository({ name: "core-extras", isPublic: true });
const privateRepository = aRepository({ name: "core-extras", isPublic: false });

const renamed =
  "Renaming it to extras: every pacman.conf using this repository must change its section name to [extras], or pacman will stop finding it.";
const madePublic =
  "Making it public: anyone, signed in or not, will be able to read every package in this repository.";
const madePrivate =
  "Making it private: clients that read this repository without credentials will lose access.";

const meta = {
  component: ConfirmRepositoryChangesDialog,
  args: {
    open: true,
    repository: publicRepository,
    edited: aRepository({ ...publicRepository, name: "extras" }),
    onConfirm: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof ConfirmRepositoryChangesDialog>;

export default meta;

type Story = StoryObj<typeof meta>;

/**
 * Finds the open dialog, which MUI renders in a portal outside the story's canvas, and asserts it
 * names the stored repository and lists exactly `consequences`, in order.
 */
async function expectConsequences(
  consequences: string[],
): Promise<HTMLElement> {
  const dialog = await screen.findByRole("dialog", {
    name: "Save these changes?",
  });
  await expect(dialog).toHaveTextContent(
    "Clients already using core-extras will be affected:",
  );
  const list = within(dialog).getByRole("list", { name: "Consequences" });
  const items = within(list).getAllByRole("listitem");
  await expect(items.map((item) => item.textContent)).toEqual(consequences);
  return dialog;
}

export const Rename: Story = {
  play: async ({ args }) => {
    const dialog = await expectConsequences([renamed]);

    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));
    await expect(args.onConfirm).toHaveBeenCalledOnce();

    await userEvent.click(
      within(dialog).getByRole("button", { name: "Cancel" }),
    );
    await expect(args.onCancel).toHaveBeenCalledOnce();
  },
};

export const MakePublic: Story = {
  args: {
    repository: privateRepository,
    edited: aRepository({ ...privateRepository, isPublic: true }),
  },
  play: async () => {
    await expectConsequences([madePublic]);
  },
};

export const MakePrivate: Story = {
  args: {
    repository: publicRepository,
    edited: aRepository({ ...publicRepository, isPublic: false }),
  },
  play: async () => {
    await expectConsequences([madePrivate]);
  },
};

export const RenameAndMakePrivate: Story = {
  args: {
    repository: publicRepository,
    edited: aRepository({
      ...publicRepository,
      name: "extras",
      isPublic: false,
    }),
  },
  play: async () => {
    await expectConsequences([renamed, madePrivate]);
  },
};

export const RenameAndMakePublic: Story = {
  args: {
    repository: privateRepository,
    edited: aRepository({
      ...privateRepository,
      name: "extras",
      isPublic: true,
    }),
  },
  play: async () => {
    await expectConsequences([renamed, madePublic]);
  },
};

export const Pending: Story = {
  args: {
    edited: aRepository({
      ...publicRepository,
      name: "extras",
      isPublic: false,
    }),
    pending: true,
  },
  play: async ({ args }) => {
    const dialog = await expectConsequences([renamed, madePrivate]);
    await expect(
      within(dialog).getByRole("button", { name: "Cancel" }),
    ).toBeDisabled();
    await expect(
      within(dialog).getByRole("button", { name: "Save" }),
    ).toBeDisabled();

    // A dialog whose update is in flight cannot be dismissed.
    await userEvent.keyboard("{Escape}");
    await expect(args.onCancel).not.toHaveBeenCalled();
    await expect(args.onConfirm).not.toHaveBeenCalled();
  },
};
