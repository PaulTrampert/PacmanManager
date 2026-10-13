import Button from "@mui/material/Button";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { Link as RouterLink } from "@tanstack/react-router";
import { expect, fn } from "storybook/test";
import { EmptyState } from "./EmptyState";

const meta = {
  component: EmptyState,
} satisfies Meta<typeof EmptyState>;

export default meta;

type Story = StoryObj<typeof meta>;

const onClearFilters = fn();

/** An action that links somewhere, as the signed-in, unfiltered list's **Create a repository**. */
export const WithLinkAction: Story = {
  args: {
    heading: "No repositories yet",
    message: "Create a repository to start publishing packages.",
    action: (
      <Button variant="contained" component={RouterLink} to="/repositories/new">
        Create a repository
      </Button>
    ),
  },
  play: async ({ canvas }) => {
    await expect(
      canvas.getByRole("heading", { level: 2, name: "No repositories yet" }),
    ).toBeVisible();
    await expect(
      canvas.getByText("Create a repository to start publishing packages."),
    ).toBeVisible();
    await expect(
      await canvas.findByRole("link", { name: "Create a repository" }),
    ).toHaveAttribute("href", "/repositories/new");
  },
};

/** An action that calls back, as the filtered list's **Clear filters**. */
export const WithButtonAction: Story = {
  args: {
    heading: "No repositories match",
    message: "Nothing matches the current filters.",
    action: (
      <Button variant="outlined" onClick={onClearFilters}>
        Clear filters
      </Button>
    ),
  },
  beforeEach: () => {
    onClearFilters.mockClear();
  },
  play: async ({ canvas, userEvent }) => {
    await expect(
      canvas.getByRole("heading", { level: 2, name: "No repositories match" }),
    ).toBeVisible();

    await userEvent.click(
      canvas.getByRole("button", { name: "Clear filters" }),
    );

    await expect(onClearFilters).toHaveBeenCalledOnce();
  },
};

/** No action, as the anonymous, unfiltered list. */
export const WithoutAction: Story = {
  args: {
    heading: "No public repositories",
    message: "There are no public repositories to show.",
  },
  play: async ({ canvas }) => {
    await expect(
      canvas.getByRole("heading", { level: 2, name: "No public repositories" }),
    ).toBeVisible();
    await expect(
      canvas.getByText("There are no public repositories to show."),
    ).toBeVisible();
    await expect(canvas.queryByRole("button")).not.toBeInTheDocument();
    await expect(canvas.queryByRole("link")).not.toBeInTheDocument();
  },
};
