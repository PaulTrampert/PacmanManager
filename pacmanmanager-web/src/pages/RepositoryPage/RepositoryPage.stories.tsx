import type { Meta, StoryObj } from "@storybook/react-vite";
import { HttpResponse } from "msw";
import { expect, fn, screen, waitFor, within } from "storybook/test";
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
    onDeleted: fn(() => Promise.resolve()),
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

/** The delete dialog, which MUI renders in a portal outside the story's canvas. */
function findDeleteDialog(): Promise<HTMLElement> {
  return screen.findByRole("dialog", { name: "Delete core-extras?" });
}

/** The delete dialog's confirmation field. */
function nameField(dialog: HTMLElement): HTMLElement {
  return within(dialog).getByRole("textbox", {
    name: "Type core-extras to confirm",
  });
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

    // Delete opens the dialog, and Cancel closes it without deleting.
    await userEvent.click(canvas.getByRole("button", { name: "Delete" }));
    const dialog = await findDeleteDialog();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Cancel" }),
    );
    await waitFor(() => expect(dialog).not.toBeInTheDocument());
    await expect(args.onDeleted).not.toHaveBeenCalled();

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

/** Typing the name and confirming deletes the repository, then calls `onDeleted`. */
export const Deleted: Story = {
  beforeEach: ({ msw }) => {
    msw.use(
      ...answerWith(coreExtras()),
      apiHandler("delete", "/v1/Repositories/{id}", ({ params }) =>
        params.id === repositoryId
          ? new HttpResponse(null, { status: 204 })
          : HttpResponse.json({ status: 404 }, { status: 404 }),
      ),
    );
  },
  play: async ({ args, canvas, userEvent }) => {
    await expectRepository(canvas, "Public");
    await userEvent.click(canvas.getByRole("button", { name: "Delete" }));
    const dialog = await findDeleteDialog();
    await userEvent.type(nameField(dialog), "core-extras");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Delete" }),
    );

    await waitFor(() => expect(args.onDeleted).toHaveBeenCalledOnce());
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();
  },
};

/**
 * The `DELETE` fails: the dialog closes, the alert shows above the header, and reopening the dialog
 * starts with an empty name field (delete-failed).
 */
export const DeleteFailed: Story = {
  beforeEach: ({ msw }) => {
    msw.use(
      ...answerWith(coreExtras()),
      apiHandler("delete", "/v1/Repositories/{id}", () =>
        HttpResponse.json(
          {
            title: "An error occurred on the server.",
            detail: "The server couldn't be reached. Try again.",
            status: 500,
          },
          { status: 500 },
        ),
      ),
    );
  },
  play: async ({ args, canvas, userEvent }) => {
    await expectRepository(canvas, "Public");
    await userEvent.click(canvas.getByRole("button", { name: "Delete" }));
    const dialog = await findDeleteDialog();
    await userEvent.type(nameField(dialog), "core-extras");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Delete" }),
    );

    const alert = await canvas.findByRole("alert");
    await waitFor(() => expect(dialog).not.toBeInTheDocument());
    await expect(alert).toHaveTextContent("core-extras was not deleted");
    await expect(alert).toHaveTextContent(
      "The server couldn't be reached. Try again.",
    );
    // Above the header, whose first line is the breadcrumbs.
    const trail = canvas.getByRole("navigation", { name: "breadcrumb" });
    await expect(
      alert.compareDocumentPosition(trail) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    await expect(args.onDeleted).not.toHaveBeenCalled();

    // Retrying means typing the name again.
    await userEvent.click(canvas.getByRole("button", { name: "Delete" }));
    const reopened = await findDeleteDialog();
    await expect(nameField(reopened)).toHaveValue("");
    await expect(
      within(reopened).getByRole("button", { name: "Delete" }),
    ).toBeDisabled();
    await userEvent.click(
      within(reopened).getByRole("button", { name: "Cancel" }),
    );
    await waitFor(() => expect(reopened).not.toBeInTheDocument());

    // The alert can be dismissed.
    await userEvent.click(within(alert).getByRole("button", { name: "Close" }));
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();
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
