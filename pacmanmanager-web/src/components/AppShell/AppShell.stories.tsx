import Typography from "@mui/material/Typography";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";
import { AppShell } from "./AppShell";

const meta = {
  component: AppShell,
  args: {
    children: <Typography>Page content</Typography>,
  },
} satisfies Meta<typeof AppShell>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {
  play: async ({ canvas }) => {
    const home = await canvas.findByRole("link", { name: "PacmanManager" });
    await expect(home).toHaveAttribute("href", "/");
    await expect(canvas.getByRole("main")).toHaveTextContent("Page content");
  },
};
