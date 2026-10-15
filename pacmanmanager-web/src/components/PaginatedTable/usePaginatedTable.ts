import type { SortDirection } from "../../api";
import type { AppliedSort, Pagination, RequestedSort } from "./PaginatedTable";

/** What {@link usePaginatedTable} needs from the table's props. */
export interface UsePaginatedTableOptions<TSortField extends string> {
  /** The sort the rows were actually ordered by; absent before the first response. */
  appliedSort?: AppliedSort<TSortField>;
  /** Called with the sort a header click asks for. */
  onSortChange: (sort: RequestedSort<TSortField>) => void;
  /** The index of the page's first row in the whole listing. */
  offset: number;
  /** The number of rows a page holds. */
  pageSize: number;
  /** Called with the page asked for. */
  onPaginationChange: (pagination: Pagination) => void;
}

/** How one column's header shows the sort, in MUI's terms. */
export interface HeaderSort {
  /** Whether the listing is ordered by the column's sort field. */
  active: boolean;
  /** The direction the header's arrow points; `"asc"` when the column is not active. */
  direction: "asc" | "desc";
  /** The header cell's `sortDirection`: the direction when active, `false` otherwise. */
  sortDirection: "asc" | "desc" | false;
}

/** What {@link usePaginatedTable} gives the table to render. */
export interface PaginatedTableBehaviour<TSortField extends string> {
  /** How the header of a column with the given sort field shows the sort. */
  headerSort: (sortField: TSortField | undefined) => HeaderSort;
  /** Asks for the sort a click on the header of the given sort field means. */
  sortBy: (sortField: TSortField) => void;
  /** The zero-based index of the page shown. */
  page: number;
  /** Asks for the page with the given zero-based index, at the same page size. */
  changePage: (page: number) => void;
  /** Asks for the given page size, starting again at the first row. */
  changePageSize: (pageSize: number | string) => void;
}

const muiDirection = {
  Ascending: "asc",
  Descending: "desc",
} as const satisfies Record<SortDirection, "asc" | "desc">;

const reversed = {
  Ascending: "Descending",
  Descending: "Ascending",
} as const satisfies Record<SortDirection, SortDirection>;

/**
 * The behaviour of a `PaginatedTable`: which header is active and which way it points, what a
 * header click asks for, and the page and page-size changes, all reported through the callbacks.
 */
export function usePaginatedTable<TSortField extends string>({
  appliedSort,
  onSortChange,
  offset,
  pageSize,
  onPaginationChange,
}: UsePaginatedTableOptions<TSortField>): PaginatedTableBehaviour<TSortField> {
  return {
    headerSort: (sortField) => {
      const active =
        appliedSort !== undefined && sortField === appliedSort.sortBy;
      const direction = active ? muiDirection[appliedSort.direction] : "asc";
      return { active, direction, sortDirection: active ? direction : false };
    },
    sortBy: (sortField) => {
      onSortChange(
        appliedSort !== undefined && sortField === appliedSort.sortBy
          ? { sortBy: sortField, direction: reversed[appliedSort.direction] }
          : { sortBy: sortField },
      );
    },
    page: Math.floor(offset / pageSize),
    changePage: (page) =>
      onPaginationChange({ offset: page * pageSize, pageSize }),
    changePageSize: (newPageSize) =>
      onPaginationChange({ offset: 0, pageSize: Number(newPageSize) }),
  };
}
