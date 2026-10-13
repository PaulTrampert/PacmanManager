import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";
import { PageBreadcrumbs } from "./PageBreadcrumbs";

const meta = {
  component: PageBreadcrumbs,
} satisfies Meta<typeof PageBreadcrumbs>;

export default meta;

type Story = StoryObj<typeof meta>;

export const CurrentPageOnly: Story = {
  args: { trail: ["Repositories"] },
  play: async ({ canvas }) => {
    await expect(
      await canvas.findByRole("navigation", { name: "breadcrumb" }),
    ).toHaveTextContent("Repositories");
    await expect(canvas.queryByRole("link")).not.toBeInTheDocument();
  },
};

export const WithAncestors: Story = {
  args: {
    // The story's router has only `/`, so every crumb links there.
    trail: [
      { label: "Repositories", link: { to: "/" } },
      { label: "core-extras", link: { to: "/" } },
      "Edit",
    ],
  },
  play: async ({ canvas }) => {
    const trail = await canvas.findByRole("navigation", {
      name: "breadcrumb",
    });
    await expect(
      canvas.getByRole("link", { name: "Repositories" }),
    ).toHaveAttribute("href", "/");
    await expect(
      canvas.getByRole("link", { name: "core-extras" }),
    ).toHaveAttribute("href", "/");
    await expect(
      canvas.queryByRole("link", { name: "Edit" }),
    ).not.toBeInTheDocument();
    await expect(trail).toHaveTextContent("Edit");
  },
};
