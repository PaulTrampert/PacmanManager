import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn } from "storybook/test";
import { PageHeader } from "./PageHeader";

const meta = {
  component: PageHeader,
  args: {
    title: "Repositories",
  },
} satisfies Meta<typeof PageHeader>;

export default meta;

type Story = StoryObj<typeof meta>;

export const TitleOnly: Story = {
  play: async ({ canvas }) => {
    await expect(
      await canvas.findByRole("heading", { level: 1, name: "Repositories" }),
    ).toBeVisible();
    await expect(
      canvas.queryByRole("navigation", { name: "breadcrumb" }),
    ).not.toBeInTheDocument();
  },
};

const onNew = fn();

export const WithActions: Story = {
  args: {
    actions: (
      <Button variant="contained" onClick={onNew}>
        New repository
      </Button>
    ),
  },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(
      await canvas.findByRole("button", { name: "New repository" }),
    );
    await expect(onNew).toHaveBeenCalledOnce();
  },
};

export const WithBreadcrumbs: Story = {
  args: {
    title: "Edit repository",
    // The story's router has only `/`, so every crumb links there.
    breadcrumbs: [
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
    await expect(
      canvas.getByRole("heading", { level: 1, name: "Edit repository" }),
    ).toBeVisible();
  },
};

export const WithAdornment: Story = {
  args: {
    title: "core-extras",
    adornment: (
      <Chip label="Public" color="success" variant="outlined" size="small" />
    ),
  },
  play: async ({ canvas }) => {
    await expect(
      await canvas.findByRole("heading", { level: 1, name: "core-extras" }),
    ).toBeVisible();
    await expect(canvas.getByText("Public")).toBeVisible();
  },
};
