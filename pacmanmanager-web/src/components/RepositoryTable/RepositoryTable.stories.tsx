import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, mocked, within } from "storybook/test";
import type { RepositorySortField } from "../../api";
import { formatDate } from "../../formatDate";
import {
  aPage,
  aRepository,
  anArchitecture,
  aUser,
} from "../../mocks/fixtures";
import type { RequestedSort } from "../PaginatedTable";
import { RepositoryTable } from "./RepositoryTable";

const paul = aUser({
  id: "11111111-1111-4111-8111-111111111111",
  displayName: "Paul Trampert",
});
const sam = aUser({
  id: "22222222-2222-4222-8222-222222222222",
  displayName: "Sam Lee",
});
const alex = aUser({
  id: "33333333-3333-4333-8333-333333333333",
  displayName: "Alex Morgan",
});

function owner({ id, displayName }: ReturnType<typeof aUser>) {
  return { id, displayName };
}

// Dates are at midday UTC, so each falls on the same day in any time zone the stories run in.
const repositories = [
  aRepository({
    id: "a0000000-0000-4000-8000-000000000001",
    name: "core-extras",
    owner: owner(paul),
    isPublic: true,
    createdAt: "2026-03-02T12:00:00Z",
    updatedAt: "2026-10-10T12:00:00Z",
  }),
  aRepository({
    id: "a0000000-0000-4000-8000-000000000002",
    name: "games",
    owner: owner(sam),
    isPublic: true,
    createdAt: "2026-06-18T12:00:00Z",
    updatedAt: "2026-09-30T12:00:00Z",
  }),
  aRepository({
    id: "a0000000-0000-4000-8000-000000000003",
    name: "my-aur-builds",
    owner: owner(paul),
    isPublic: false,
    supportedArchitectures: ["x86_64", "riscv64"],
    createdAt: "2026-04-11T12:00:00Z",
    updatedAt: "2026-10-12T12:00:00Z",
  }),
  aRepository({
    id: "a0000000-0000-4000-8000-000000000004",
    name: "paul-dev",
    owner: owner(paul),
    isPublic: false,
    createdAt: "2026-08-03T12:00:00Z",
    updatedAt: "2026-08-03T12:00:00Z",
  }),
  aRepository({
    id: "a0000000-0000-4000-8000-000000000005",
    name: "team-tools",
    owner: owner(alex),
    isPublic: true,
    createdAt: "2026-05-27T12:00:00Z",
    updatedAt: "2026-10-01T12:00:00Z",
  }),
] as const;

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
];

const meta = {
  component: RepositoryTable,
  args: {
    response: aPage(repositories),
    loading: false,
    pageSize: 50,
    architectures,
    // The story's router has only `/`, so each name links there, its repository's id in the hash.
    repositoryLink: (repository) => ({ to: "/", hash: repository.id }),
    onSortChange: fn(),
    onPaginationChange: fn(),
  },
} satisfies Meta<typeof RepositoryTable>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The one sort a click reported, compared strictly so that an absent `direction` is not `undefined`. */
async function expectReportedSort(
  onSortChange: (sort: RequestedSort<RepositorySortField>) => void,
  expected: RequestedSort<RepositorySortField>,
) {
  await expect(onSortChange).toHaveBeenCalledOnce();
  await expect(mocked(onSortChange).mock.calls[0][0]).toStrictEqual(expected);
  mocked(onSortChange).mockClear();
}

/** The cells of the body row whose name link reads `name`. */
function rowOf(canvasElement: HTMLElement, name: string) {
  const link = within(canvasElement).getByRole("link", { name });
  return within(link.closest("tr")!);
}

export const SeveralRows: Story = {
  play: async ({ canvas, canvasElement }) => {
    const table = canvas.getByRole("table", { name: "Repositories" });
    await expect(table).toHaveAttribute("aria-busy", "false");
    await expect(
      within(table)
        .getAllByRole("columnheader")
        .map((header) => header.textContent),
    ).toEqual([
      expect.stringMatching(/^Name/),
      "Owner",
      "Visibility",
      "Architectures",
      expect.stringMatching(/^Created/),
      expect.stringMatching(/^Updated/),
    ]);
    // A header row and one row per repository.
    await expect(canvas.getAllByRole("row")).toHaveLength(
      repositories.length + 1,
    );

    // Each name links where `repositoryLink` says for its repository.
    await expect(
      canvas.getByRole("link", { name: "core-extras" }),
    ).toHaveAttribute("href", "/#a0000000-0000-4000-8000-000000000001");
    await expect(canvas.getByRole("link", { name: "games" })).toHaveAttribute(
      "href",
      "/#a0000000-0000-4000-8000-000000000002",
    );

    const coreExtras = rowOf(canvasElement, "core-extras");
    await expect(coreExtras.getByText("Paul Trampert")).toBeVisible();
    await expect(
      coreExtras.getByRole("img", { name: "Visibility: Public" }),
    ).toBeVisible();
    await expect(
      coreExtras.getByText("64-bit Intel/AMD (x86_64)"),
    ).toBeVisible();
    const [created, updated] = coreExtras
      .getAllByRole("cell")
      .slice(-2)
      .map((cell) => cell.querySelector("time"));
    await expect(created).toHaveAttribute("datetime", "2026-03-02T12:00:00Z");
    await expect(created).toHaveTextContent(formatDate("2026-03-02T12:00:00Z"));
    await expect(updated).toHaveAttribute("datetime", "2026-10-10T12:00:00Z");
    await expect(updated).toHaveTextContent(formatDate("2026-10-10T12:00:00Z"));

    // An architecture missing from the array is shown by its raw value.
    const myAurBuilds = rowOf(canvasElement, "my-aur-builds");
    await expect(
      myAurBuilds.getByRole("img", { name: "Visibility: Private" }),
    ).toBeVisible();
    await expect(
      myAurBuilds.getByText("64-bit Intel/AMD (x86_64), riscv64"),
    ).toBeVisible();

    await expect(canvas.getByText("1–5 of 5")).toBeVisible();
  },
};

/** Sorted by name, ascending, the list's default. */
export const SortedByName: Story = {
  play: async ({ args, canvas, userEvent }) => {
    await expect(
      canvas.getByRole("columnheader", { name: /^Name/ }),
    ).toHaveAttribute("aria-sort", "ascending");

    await userEvent.click(canvas.getByRole("button", { name: /^Name/ }));
    await expectReportedSort(args.onSortChange, {
      sortBy: "Name",
      direction: "Descending",
    });
    await userEvent.click(canvas.getByRole("button", { name: /^Created/ }));
    await expectReportedSort(args.onSortChange, { sortBy: "Created" });
  },
};

/** Sorted by creation, newest first, the field's default direction. */
export const SortedByCreated: Story = {
  args: {
    response: aPage([...repositories].reverse(), {
      sort: { sortBy: "Created", direction: "Descending" },
    }),
  },
  play: async ({ args, canvas, userEvent }) => {
    await expect(
      canvas.getByRole("columnheader", { name: /^Created/ }),
    ).toHaveAttribute("aria-sort", "descending");
    await expect(
      canvas.getByRole("columnheader", { name: /^Name/ }),
    ).not.toHaveAttribute("aria-sort");

    await userEvent.click(canvas.getByRole("button", { name: /^Created/ }));
    await expectReportedSort(args.onSortChange, {
      sortBy: "Created",
      direction: "Ascending",
    });
    await userEvent.click(canvas.getByRole("button", { name: /^Updated/ }));
    await expectReportedSort(args.onSortChange, { sortBy: "Updated" });
  },
};

/** Sorted by last update, oldest first. */
export const SortedByUpdated: Story = {
  args: {
    response: aPage(repositories, {
      sort: { sortBy: "Updated", direction: "Ascending" },
    }),
  },
  play: async ({ args, canvas, userEvent }) => {
    await expect(
      canvas.getByRole("columnheader", { name: /^Updated/ }),
    ).toHaveAttribute("aria-sort", "ascending");

    await userEvent.click(canvas.getByRole("button", { name: /^Updated/ }));
    await expectReportedSort(args.onSortChange, {
      sortBy: "Updated",
      direction: "Descending",
    });
    await userEvent.click(canvas.getByRole("button", { name: /^Name/ }));
    await expectReportedSort(args.onSortChange, { sortBy: "Name" });
  },
};

/** The second page of a longer list, its position taken from the response. */
export const SecondPage: Story = {
  args: {
    response: aPage(repositories, { offset: 25, total: 30 }),
    pageSize: 25,
  },
  play: async ({ args, canvas, userEvent }) => {
    await expect(canvas.getByText("26–30 of 30")).toBeVisible();

    await userEvent.click(
      canvas.getByRole("button", { name: "Go to previous page" }),
    );
    await expect(args.onPaginationChange).toHaveBeenLastCalledWith({
      offset: 0,
      pageSize: 25,
    });
  },
};

/** The next page is loading: the previous rows stay, dimmed, with a spinner over them. */
export const LoadingWithPreviousRows: Story = {
  args: { loading: true },
  play: async ({ canvas }) => {
    const table = canvas.getByRole("table", { name: "Repositories" });
    await expect(table).toHaveAttribute("aria-busy", "true");
    await expect(canvas.getAllByRole("row")).toHaveLength(
      repositories.length + 1,
    );
    await expect(
      canvas.getByRole("link", { name: "core-extras" }),
    ).toBeVisible();
    // The previous response's sort stays shown.
    await expect(
      canvas.getByRole("columnheader", { name: /^Name/ }),
    ).toHaveAttribute("aria-sort", "ascending");
    await expect(
      canvas.getByRole("progressbar", { name: "Loading" }),
    ).toBeVisible();
  },
};

/** The first page is loading: no response yet, so no rows and no sort, and a spinner in the body. */
export const FirstLoad: Story = {
  args: { loading: true, response: undefined },
  play: async ({ canvas }) => {
    const table = canvas.getByRole("table", { name: "Repositories" });
    await expect(table).toHaveAttribute("aria-busy", "true");
    // The header row and the spinner's row.
    await expect(canvas.getAllByRole("row")).toHaveLength(2);
    await expect(canvas.queryByRole("link")).not.toBeInTheDocument();
    await expect(
      canvas.getByRole("progressbar", { name: "Loading" }),
    ).toBeVisible();
    for (const name of [/^Name/, /^Created/, /^Updated/]) {
      await expect(
        canvas.getByRole("columnheader", { name }),
      ).not.toHaveAttribute("aria-sort");
    }
  },
};
