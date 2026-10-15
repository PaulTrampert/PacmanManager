import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, within } from "storybook/test";
import { formatDate } from "../../formatDate";
import { aRepository, anArchitecture, aUser } from "../../mocks/fixtures";
import { RepositoryHeader } from "./RepositoryHeader";

const paul = aUser({
  id: "11111111-1111-4111-8111-111111111111",
  displayName: "Paul Trampert",
});

// Dates are at midday UTC, so each falls on the same day in any time zone the stories run in.
const coreExtras = aRepository({
  id: "a0000000-0000-4000-8000-000000000001",
  name: "core-extras",
  owner: { id: paul.id, displayName: paul.displayName },
  isPublic: true,
  supportedArchitectures: ["x86_64"],
  createdAt: "2026-03-02T12:00:00Z",
  updatedAt: "2026-10-10T12:00:00Z",
});

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
  anArchitecture({ value: "aarch64", displayValue: "64-bit ARM (aarch64)" }),
];

const meta = {
  component: RepositoryHeader,
  args: {
    repository: coreExtras,
    architectures,
    // The story's router has only `/`; the edit link carries a hash so that the two links can be
    // told apart.
    repositoriesLink: { to: "/" },
    editLink: { to: "/", hash: "edit" },
    onDelete: fn(),
  },
} satisfies Meta<typeof RepositoryHeader>;

export default meta;

type Story = StoryObj<typeof meta>;

type Canvas = ReturnType<typeof within>;

/** Asserts the breadcrumbs, heading and chip for a repository named `name`. */
async function expectTitle(canvas: Canvas, name: string, visibility: string) {
  const trail = await canvas.findByRole("navigation", { name: "breadcrumb" });
  await expect(
    within(trail).getByRole("link", { name: "Repositories" }),
  ).toHaveAttribute("href", "/");
  await expect(
    within(trail).queryByRole("link", { name }),
  ).not.toBeInTheDocument();
  await expect(trail).toHaveTextContent(name);
  await expect(canvas.getByRole("heading", { level: 1, name })).toBeVisible();
  await expect(
    canvas.getByRole("img", { name: `Visibility: ${visibility}` }),
  ).toBeVisible();
}

/** The definition the details row gives for `term`. */
function detail(canvas: Canvas, term: string) {
  return canvas.getByText(term, { selector: "dt" }).nextElementSibling;
}

/** The owner of a public repository, who may edit and delete it. */
export const CanUpdateAndDelete: Story = {
  play: async ({ args, canvas, userEvent }) => {
    await expectTitle(canvas, "core-extras", "Public");

    await expect(canvas.getByRole("link", { name: "Edit" })).toHaveAttribute(
      "href",
      "/#edit",
    );

    await userEvent.click(canvas.getByRole("button", { name: "Delete" }));
    await expect(args.onDelete).toHaveBeenCalledOnce();

    await expect(detail(canvas, "Owner")).toHaveTextContent("Paul Trampert");
    await expect(detail(canvas, "Architectures")).toHaveTextContent(
      "64-bit Intel/AMD (x86_64)",
    );
    const created = detail(canvas, "Created")?.querySelector("time");
    await expect(created).toHaveAttribute("datetime", "2026-03-02T12:00:00Z");
    await expect(created).toHaveTextContent(formatDate("2026-03-02T12:00:00Z"));
    const updated = detail(canvas, "Updated")?.querySelector("time");
    await expect(updated).toHaveAttribute("datetime", "2026-10-10T12:00:00Z");
    await expect(updated).toHaveTextContent(formatDate("2026-10-10T12:00:00Z"));
  },
};

/**
 * The owner of a private repository supporting several architectures, one of them unknown to the
 * architectures list, which is shown by its raw value.
 */
export const Private: Story = {
  args: {
    repository: aRepository({
      ...coreExtras,
      name: "my-aur-builds",
      isPublic: false,
      supportedArchitectures: ["x86_64", "aarch64", "riscv64"],
    }),
  },
  play: async ({ canvas }) => {
    await expectTitle(canvas, "my-aur-builds", "Private");
    await expect(canvas.getByRole("link", { name: "Edit" })).toBeVisible();
    await expect(canvas.getByRole("button", { name: "Delete" })).toBeVisible();
    await expect(detail(canvas, "Architectures")).toHaveTextContent(
      "64-bit Intel/AMD (x86_64), 64-bit ARM (aarch64), riscv64",
    );
  },
};

/** A caller whose credential may update the repository but not delete it. */
export const CanUpdateOnly: Story = {
  args: {
    repository: aRepository({
      ...coreExtras,
      permissions: { update: true, delete: false },
    }),
  },
  play: async ({ canvas }) => {
    await expectTitle(canvas, "core-extras", "Public");
    await expect(canvas.getByRole("link", { name: "Edit" })).toHaveAttribute(
      "href",
      "/#edit",
    );
    await expect(
      canvas.queryByRole("button", { name: "Delete" }),
    ).not.toBeInTheDocument();
  },
};

/** A caller whose credential may delete the repository but not update it. */
export const CanDeleteOnly: Story = {
  args: {
    repository: aRepository({
      ...coreExtras,
      permissions: { update: false, delete: true },
    }),
  },
  play: async ({ args, canvas, userEvent }) => {
    await expectTitle(canvas, "core-extras", "Public");
    await expect(
      canvas.queryByRole("link", { name: "Edit" }),
    ).not.toBeInTheDocument();
    await userEvent.click(canvas.getByRole("button", { name: "Delete" }));
    await expect(args.onDelete).toHaveBeenCalledOnce();
  },
};

/** Someone other than the owner, or an anonymous visitor: neither button is shown. */
export const CanDoNeither: Story = {
  args: {
    repository: aRepository({
      ...coreExtras,
      name: "games",
      owner: {
        id: "22222222-2222-4222-8222-222222222222",
        displayName: "Sam Lee",
      },
      permissions: { update: false, delete: false },
    }),
  },
  play: async ({ canvas }) => {
    await expectTitle(canvas, "games", "Public");
    await expect(
      canvas.queryByRole("link", { name: "Edit" }),
    ).not.toBeInTheDocument();
    await expect(
      canvas.queryByRole("button", { name: "Delete" }),
    ).not.toBeInTheDocument();
    await expect(detail(canvas, "Owner")).toHaveTextContent("Sam Lee");
  },
};
