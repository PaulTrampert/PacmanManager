import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";
import { ForbiddenMessage, forbiddenText } from "./ForbiddenMessage";

const meta = {
  component: ForbiddenMessage,
} satisfies Meta<typeof ForbiddenMessage>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("alert")).toHaveTextContent(forbiddenText);
  },
};
