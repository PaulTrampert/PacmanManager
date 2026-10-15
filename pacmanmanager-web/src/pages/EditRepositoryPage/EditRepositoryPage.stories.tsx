import type { Meta, StoryObj } from "@storybook/react-vite";
import { HttpResponse } from "msw";
import { expect, fn } from "storybook/test";
import type { Repository } from "../../api";
import { forbiddenText } from "../../components/ForbiddenMessage";
import { apiHandler } from "../../mocks/api";
import { aRepository, anArchitecture } from "../../mocks/fixtures";
import { EditRepositoryPage } from "./EditRepositoryPage";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
  anArchitecture({ value: "aarch64", displayValue: "64-bit ARM (aarch64)" }),
];

/** The repository the page edits, as a caller who may update it sees it, with any field overridden. */
function coreExtras(overrides: Partial<Repository> = {}): Repository {
  return aRepository({
    id: repositoryId,
    name: "core-extras",
    isPublic: true,
    supportedArchitectures: ["x86_64"],
    ...overrides,
  });
}

/** Answers the page's queries with `repository` and the architectures. */
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

const meta = {
  component: EditRepositoryPage,
  args: {
    repositoryId,
    // The story's router has only `/`, so every crumb links there.
    repositoriesLink: { to: "/" },
    repositoryLink: { to: "/" },
    onSaved: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof EditRepositoryPage>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The wireframe edit-unchanged: the form filled in with the repository, nothing yet changed. */
export const Unchanged: Story = {
  beforeEach: ({ msw }) => {
    msw.use(...answerWith(coreExtras()));
  },
  play: async ({ canvas }) => {
    await expect(
      await canvas.findByRole("heading", {
        level: 1,
        name: "Edit repository",
      }),
    ).toBeInTheDocument();
    const trail = canvas.getByRole("navigation", { name: "breadcrumb" });
    await expect(trail).toHaveTextContent("Repositories/core-extras/Edit");
    await expect(
      canvas.getByRole("link", { name: "core-extras" }),
    ).toBeVisible();
    await expect(canvas.getByRole("textbox", { name: "Name" })).toHaveValue(
      "core-extras",
    );
    await expect(canvas.getByRole("radio", { name: "Public" })).toBeChecked();
    await expect(
      canvas.getByRole("radio", { name: "Private" }),
    ).not.toBeChecked();
    const architecturesField = canvas.getByRole("combobox", {
      name: "Architectures",
    });
    await expect(architecturesField).toHaveAttribute("aria-disabled", "true");
    await expect(architecturesField).toHaveTextContent(
      "64-bit Intel/AMD (x86_64)",
    );
    await expect(architecturesField).not.toHaveTextContent(
      "64-bit ARM (aarch64)",
    );
    await expect(canvas.getByRole("button", { name: "Save" })).toBeDisabled();
    await expect(canvas.getByRole("button", { name: "Cancel" })).toBeEnabled();
    await expect(canvas.queryByText(forbiddenText)).not.toBeInTheDocument();
  },
};

/** A repository the caller may not update: the `403` message in place of the form. */
export const Forbidden: Story = {
  beforeEach: ({ msw }) => {
    msw.use(
      ...answerWith(
        coreExtras({ permissions: { update: false, delete: false } }),
      ),
    );
  },
  play: async ({ canvas }) => {
    await expect(await canvas.findByText(forbiddenText)).toBeVisible();
    await expect(
      canvas.getByRole("heading", { level: 1, name: "Edit repository" }),
    ).toBeInTheDocument();
    await expect(
      canvas.getByRole("navigation", { name: "breadcrumb" }),
    ).toHaveTextContent("Repositories/core-extras/Edit");
    await expect(
      canvas.queryByRole("textbox", { name: "Name" }),
    ).not.toBeInTheDocument();
    await expect(
      canvas.queryByRole("button", { name: "Save" }),
    ).not.toBeInTheDocument();
  },
};
