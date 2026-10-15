import type { Meta, StoryObj } from "@storybook/react-vite";
import { HttpResponse } from "msw";
import { expect, fn, within } from "storybook/test";
import { apiHandler } from "../../mocks/api";
import { anArchitecture } from "../../mocks/fixtures";
import { NewRepositoryPage } from "./NewRepositoryPage";

const x86_64 = anArchitecture({
  value: "x86_64",
  displayValue: "64-bit Intel/AMD (x86_64)",
});

const meta = {
  component: NewRepositoryPage,
  args: {
    // The story's router has only `/`.
    repositoriesLink: { to: "/" },
    onCreated: fn(),
    onCancel: fn(),
  },
  beforeEach: ({ msw }) => {
    msw.use(
      apiHandler("get", "/v1/Architectures", () => HttpResponse.json([x86_64])),
    );
  },
} satisfies Meta<typeof NewRepositoryPage>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The page as it opens: breadcrumbs, heading, and the form empty (new-empty). */
export const Empty: Story = {
  play: async ({ canvas }) => {
    const trail = await canvas.findByRole("navigation", { name: "breadcrumb" });
    await expect(
      within(trail).getByRole("link", { name: "Repositories" }),
    ).toHaveAttribute("href", "/");
    await expect(within(trail).getByText("New repository")).toHaveAttribute(
      "aria-current",
      "page",
    );
    await expect(
      canvas.getByRole("heading", { level: 1, name: "New repository" }),
    ).toBeVisible();

    await expect(canvas.getByRole("textbox", { name: "Name" })).toHaveValue("");
    await expect(canvas.getByRole("radio", { name: "Private" })).toBeChecked();
    await expect(
      canvas.getByRole("radio", { name: "Public" }),
    ).not.toBeChecked();
    const architectures = canvas.getByRole("combobox", {
      name: "Architectures",
    });
    await expect(architectures).toHaveTextContent("64-bit Intel/AMD (x86_64)");
    await expect(architectures).toHaveAttribute("aria-disabled", "true");
    await expect(canvas.getByRole("button", { name: "Create" })).toBeEnabled();
    await expect(canvas.getByRole("button", { name: "Cancel" })).toBeEnabled();
  },
};
