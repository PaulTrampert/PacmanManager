import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, mocked, screen } from "storybook/test";
import {
  PaginatedTable,
  type PaginatedTableColumn,
  type RequestedSort,
} from "./PaginatedTable";

// The table is generic, so its stories use a row of their own rather than an API type.
interface Item {
  name: string;
  owner: string;
  created: string;
}

type ItemSortField = "Name" | "Created";

const columns: PaginatedTableColumn<Item, ItemSortField>[] = [
  { id: "name", header: "Name", sortField: "Name", render: (row) => row.name },
  { id: "owner", header: "Owner", render: (row) => row.owner },
  {
    id: "created",
    header: "Created",
    sortField: "Created",
    render: (row) => row.created,
  },
];

const rows: Item[] = [
  { name: "core-extras", owner: "Paul Trampert", created: "Mar 2, 2026" },
  { name: "games", owner: "Sam Lee", created: "Jun 18, 2026" },
  { name: "my-aur-builds", owner: "Paul Trampert", created: "Apr 11, 2026" },
  { name: "paul-dev", owner: "Paul Trampert", created: "Aug 3, 2026" },
  { name: "team-tools", owner: "Alex Morgan", created: "May 27, 2026" },
];

// A story's component cannot take type arguments, so the instantiation is named as a type.
const ItemTable: typeof PaginatedTable<Item, ItemSortField> = PaginatedTable;

const meta = {
  component: ItemTable,
  args: {
    columns,
    rows,
    getRowKey: (row) => row.name,
    appliedSort: { sortBy: "Name", direction: "Ascending" },
    onSortChange: fn(),
    offset: 0,
    pageSize: 50,
    total: rows.length,
    onPaginationChange: fn(),
    "aria-label": "Items",
  },
} satisfies Meta<typeof ItemTable>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The one sort a click reported, compared strictly so that an absent `direction` is not `undefined`. */
async function expectReportedSort(
  onSortChange: (sort: RequestedSort<ItemSortField>) => void,
  expected: RequestedSort<ItemSortField>,
) {
  await expect(onSortChange).toHaveBeenCalledOnce();
  await expect(mocked(onSortChange).mock.calls[0][0]).toStrictEqual(expected);
  mocked(onSortChange).mockClear();
}

export const SeveralRows: Story = {
  play: async ({ canvas }) => {
    const table = canvas.getByRole("table", { name: "Items" });
    // A header row and one row per item.
    await expect(canvas.getAllByRole("row")).toHaveLength(rows.length + 1);
    await expect(table).toHaveTextContent("core-extras");
    await expect(table).toHaveTextContent("Alex Morgan");
    // Only the columns with a sort field can be sorted on.
    await expect(
      canvas.getByRole("button", { name: /^Name/ }),
    ).toBeInTheDocument();
    await expect(
      canvas.getByRole("button", { name: /^Created/ }),
    ).toBeInTheDocument();
    await expect(
      canvas.queryByRole("button", { name: /^Owner/ }),
    ).not.toBeInTheDocument();
    await expect(canvas.getByText("1–5 of 5")).toBeVisible();
  },
};

export const AscendingSort: Story = {
  args: { appliedSort: { sortBy: "Created", direction: "Ascending" } },
  play: async ({ args, canvas, userEvent }) => {
    await expect(
      canvas.getByRole("columnheader", { name: /^Created/ }),
    ).toHaveAttribute("aria-sort", "ascending");
    await expect(
      canvas.getByRole("columnheader", { name: /^Name/ }),
    ).not.toHaveAttribute("aria-sort");

    // The active header reverses the direction.
    await userEvent.click(canvas.getByRole("button", { name: /^Created/ }));
    await expectReportedSort(args.onSortChange, {
      sortBy: "Created",
      direction: "Descending",
    });

    // Another header is reported with no direction, so the field's default applies.
    await userEvent.click(canvas.getByRole("button", { name: /^Name/ }));
    await expectReportedSort(args.onSortChange, { sortBy: "Name" });
  },
};

export const DescendingSort: Story = {
  args: { appliedSort: { sortBy: "Name", direction: "Descending" } },
  play: async ({ args, canvas, userEvent }) => {
    await expect(
      canvas.getByRole("columnheader", { name: /^Name/ }),
    ).toHaveAttribute("aria-sort", "descending");
    await expect(
      canvas.getByRole("columnheader", { name: /^Created/ }),
    ).not.toHaveAttribute("aria-sort");

    await userEvent.click(canvas.getByRole("button", { name: /^Name/ }));
    await expectReportedSort(args.onSortChange, {
      sortBy: "Name",
      direction: "Ascending",
    });

    await userEvent.click(canvas.getByRole("button", { name: /^Created/ }));
    await expectReportedSort(args.onSortChange, { sortBy: "Created" });
  },
};

export const SecondPage: Story = {
  args: { offset: 50, pageSize: 50, total: 120 },
  play: async ({ args, canvas, userEvent }) => {
    await expect(canvas.getByText("51–100 of 120")).toBeVisible();

    await userEvent.click(
      canvas.getByRole("button", { name: "Go to next page" }),
    );
    await expect(args.onPaginationChange).toHaveBeenLastCalledWith({
      offset: 100,
      pageSize: 50,
    });

    await userEvent.click(
      canvas.getByRole("button", { name: "Go to previous page" }),
    );
    await expect(args.onPaginationChange).toHaveBeenLastCalledWith({
      offset: 0,
      pageSize: 50,
    });

    // A new page size starts again at the first page.
    await userEvent.click(canvas.getByRole("combobox"));
    await userEvent.click(await screen.findByRole("option", { name: "25" }));
    await expect(args.onPaginationChange).toHaveBeenLastCalledWith({
      offset: 0,
      pageSize: 25,
    });
    await expect(args.onPaginationChange).toHaveBeenCalledTimes(3);
  },
};

export const CustomPageSizes: Story = {
  args: { pageSize: 10, rowsPerPageOptions: [10, 20] },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("combobox"));
    const options = await screen.findAllByRole("option");
    await expect(options.map((option) => option.textContent)).toEqual([
      "10",
      "20",
    ]);
    await userEvent.keyboard("{Escape}");
  },
};
