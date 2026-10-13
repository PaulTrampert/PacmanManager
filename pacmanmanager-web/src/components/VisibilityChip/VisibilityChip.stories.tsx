import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";
import { VisibilityChip } from "./VisibilityChip";

const meta = {
  component: VisibilityChip,
} satisfies Meta<typeof VisibilityChip>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Public: Story = {
  args: { isPublic: true },
  play: async ({ canvas }) => {
    const chip = canvas.getByRole("img", { name: "Visibility: Public" });
    await expect(chip).toHaveTextContent("Public");
  },
};

export const Private: Story = {
  args: { isPublic: false },
  play: async ({ canvas }) => {
    const chip = canvas.getByRole("img", { name: "Visibility: Private" });
    await expect(chip).toHaveTextContent("Private");
  },
};
