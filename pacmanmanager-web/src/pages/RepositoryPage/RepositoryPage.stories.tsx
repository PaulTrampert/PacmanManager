import type { Meta, StoryObj } from "@storybook/react-vite";
import { HttpResponse } from "msw";
import { expect, fn, within } from "storybook/test";
import type { Repository } from "../../api";
import { apiHandler } from "../../mocks/api";
import { aRepository, anArchitecture, aUser } from "../../mocks/fixtures";
import { RepositoryPage } from "./RepositoryPage";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

const paul = aUser({
  id: "11111111-1111-4111-8111-111111111111",
  displayName: "Paul Trampert",
});

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
  anArchitecture({ value: "aarch64", displayValue: "64-bit ARM (aarch64)" }),
];

// Dates are at midday UTC, so each falls on the same day in any time zone the stories run in.
function coreExtras(overrides: Partial<Repository> = {}): Repository {
  return aRepository({
    id: repositoryId,
    name: "core-extras",
    owner: { id: paul.id, displayName: paul.displayName },
    supportedArchitectures: ["x86_64", "aarch64"],
    createdAt: "2026-03-02T12:00:00Z",
    updatedAt: "2026-10-10T12:00:00Z",
    serverUrl: "https://pacman.example.com/pacman/core-extras/$arch",
    ...overrides,
  });
}

/** Answers the page's requests with `repository` and the architectures. */
function answerWith(repository: Repository) {
  return [
    apiHandler("get", "/v1/Repositories/{id}", () =>
      HttpResponse.json(repository),
    ),
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json(architectures),
    ),
  ];
}

/** As the caller who may neither update nor delete the repository sees it. */
const readOnly = coreExtras({ permissions: { update: false, delete: false } });

const meta = {
  component: RepositoryPage,
  args: {
    repositoryId,
    // The story's router has only `/`; the links carry hashes so that they can be told apart.
    repositoriesLink: { to: "/", hash: "repositories" },
    editLink: { to: "/", hash: "edit" },
    onDelete: fn(),
  },
} satisfies Meta<typeof RepositoryPage>;

export default meta;

type Story = StoryObj<typeof meta>;

type Canvas = ReturnType<typeof within>;

/** Asserts the title, breadcrumbs, details and collapsed `pacman.conf` section. */
async function expectRepository(canvas: Canvas, visibility: string) {
  await expect(
    await canvas.findByRole("heading", { level: 1, name: "core-extras" }),
  ).toBeVisible();
  const trail = canvas.getByRole("navigation", { name: "breadcrumb" });
  await expect(
    within(trail).getByRole("link", { name: "Repositories" }),
  ).toHaveAttribute("href", "/#repositories");
  await expect(
    canvas.getByRole("img", { name: `Visibility: ${visibility}` }),
  ).toBeVisible();
  await expect(
    canvas.getByText("Owner", { selector: "dt" }).nextElementSibling,
  ).toHaveTextContent("Paul Trampert");
  await expect(
    canvas.getByText("Architectures", { selector: "dt" }).nextElementSibling,
  ).toHaveTextContent("64-bit Intel/AMD (x86_64), 64-bit ARM (aarch64)");
  await expect(
    canvas.getByRole("button", { name: /Use this repository/ }),
  ).toHaveAttribute("aria-expanded", "false");
}

/** Its owner, on a public repository: Edit and Delete, and the section once expanded. */
export const OwnerPublic: Story = {
  beforeEach: ({ msw }) => {
    msw.use(...answerWith(coreExtras()));
  },
  play: async ({ args, canvas, userEvent }) => {
    await expectRepository(canvas, "Public");
    await expect(canvas.getByRole("link", { name: "Edit" })).toHaveAttribute(
      "href",
      "/#edit",
    );

    await userEvent.click(canvas.getByRole("button", { name: "Delete" }));
    await expect(args.onDelete).toHaveBeenCalledOnce();

    await userEvent.click(
      canvas.getByRole("button", { name: /Use this repository/ }),
    );
    await expect(await canvas.findByText(/^\[core-extras\]/)).toHaveTextContent(
      "[core-extras] SigLevel = Optional TrustAll Server = https://pacman.example.com/pacman/core-extras/$arch",
    );
  },
};

/** Its owner, on a private repository: the section carries the private note. */
export const OwnerPrivate: Story = {
  beforeEach: ({ msw }) => {
    msw.use(...answerWith(coreExtras({ isPublic: false })));
  },
  play: async ({ canvas, userEvent }) => {
    await expectRepository(canvas, "Private");
    await expect(canvas.getByRole("link", { name: "Edit" })).toBeVisible();
    await expect(canvas.getByRole("button", { name: "Delete" })).toBeVisible();

    await userEvent.click(
      canvas.getByRole("button", { name: /Use this repository/ }),
    );
    await expect(
      await canvas.findByText(/pacman must authenticate to read it/),
    ).toBeVisible();
  },
};

/** A signed-in user who does not own it: no Edit or Delete. */
export const NotOwner: Story = {
  beforeEach: ({ msw }) => {
    msw.use(...answerWith(readOnly));
  },
  play: async ({ canvas }) => {
    await expectRepository(canvas, "Public");
    await expect(
      canvas.queryByRole("link", { name: "Edit" }),
    ).not.toBeInTheDocument();
    await expect(
      canvas.queryByRole("button", { name: "Delete" }),
    ).not.toBeInTheDocument();
  },
};

/**
 * An anonymous visitor, who sees only public repositories and may change none. The page shows the
 * same as for a signed-in user who does not own it; only the app bar, outside the page, differs.
 */
export const Anonymous: Story = {
  beforeEach: ({ msw }) => {
    msw.use(...answerWith(readOnly));
  },
  play: async ({ canvas }) => {
    await expectRepository(canvas, "Public");
    await expect(
      canvas.queryByRole("link", { name: "Edit" }),
    ).not.toBeInTheDocument();
    await expect(
      canvas.queryByRole("button", { name: "Delete" }),
    ).not.toBeInTheDocument();
  },
};
