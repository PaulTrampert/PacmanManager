import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TablePagination from "@mui/material/TablePagination";
import TableRow from "@mui/material/TableRow";
import TableSortLabel from "@mui/material/TableSortLabel";
import type { Key, ReactNode } from "react";
import type { SortDirection } from "../../api";

/** One column of a {@link PaginatedTable}. */
export interface PaginatedTableColumn<TRow, TSortField extends string> {
  /** Identifies the column among its siblings; never shown. */
  id: string;
  /** The column's header. */
  header: ReactNode;
  /** Renders the column's cell for one row. */
  render: (row: TRow) => ReactNode;
  /** The sort field the column's header sorts by. A column without one cannot be sorted on. */
  sortField?: TSortField;
}

/** The sort a listing was actually ordered by, with the field's default direction resolved. */
export interface AppliedSort<TSortField extends string> {
  /** The field the listing is ordered by. */
  sortBy: TSortField;
  /** The direction the listing is ordered in. */
  direction: SortDirection;
}

/**
 * The sort a header click asks for. `direction` is absent when another column was chosen, so that
 * the field's default direction applies.
 */
export interface RequestedSort<TSortField extends string> {
  /** The field to order by. */
  sortBy: TSortField;
  /** The direction to order in; absent for the field's default. */
  direction?: SortDirection;
}

/** Which page of a listing to show, and how many rows a page holds. */
export interface Pagination {
  /** The index of the page's first row in the whole listing. */
  offset: number;
  /** The number of rows a page holds. */
  pageSize: number;
}

/** The page sizes a {@link PaginatedTable} offers unless it is given others. */
export const defaultRowsPerPageOptions: readonly number[] = [25, 50, 100];

/** The props {@link PaginatedTable} takes. */
export interface PaginatedTableProps<TRow, TSortField extends string> {
  /** The table's columns, in order. */
  columns: readonly PaginatedTableColumn<TRow, TSortField>[];
  /** The rows of the page shown. */
  rows: readonly TRow[];
  /** A key identifying a row among the page's rows. */
  getRowKey: (row: TRow) => Key;
  /**
   * The sort the rows were actually ordered by — the response's, never the one requested — which
   * marks the active header and its arrow.
   */
  appliedSort: AppliedSort<TSortField>;
  /**
   * Called when a sortable header is clicked: the active one with its direction reversed, any other
   * with no direction.
   */
  onSortChange: (sort: RequestedSort<TSortField>) => void;
  /** The index of the page's first row in the whole listing. */
  offset: number;
  /** The number of rows a page holds; one of `rowsPerPageOptions`. */
  pageSize: number;
  /** The number of rows in the whole listing. */
  total: number;
  /** Called with the page asked for; a new page size always starts again at offset `0`. */
  onPaginationChange: (pagination: Pagination) => void;
  /** The page sizes offered; 25, 50 and 100 unless given. */
  rowsPerPageOptions?: readonly number[];
  /** The table's accessible name. */
  "aria-label"?: string;
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
 * An outlined table of one page of a listing, sortable from its column headers and paged beneath.
 * It is presentational: the sort and the page are props, and changes to them are reported through
 * `onSortChange` and `onPaginationChange` for the caller to apply.
 */
export function PaginatedTable<TRow, TSortField extends string>({
  columns,
  rows,
  getRowKey,
  appliedSort,
  onSortChange,
  offset,
  pageSize,
  total,
  onPaginationChange,
  rowsPerPageOptions = defaultRowsPerPageOptions,
  "aria-label": ariaLabel,
}: PaginatedTableProps<TRow, TSortField>) {
  const sortBy = (sortField: TSortField) => {
    onSortChange(
      sortField === appliedSort.sortBy
        ? { sortBy: sortField, direction: reversed[appliedSort.direction] }
        : { sortBy: sortField },
    );
  };

  return (
    <Paper variant="outlined">
      <TableContainer>
        <Table aria-label={ariaLabel}>
          <TableHead>
            <TableRow>
              {columns.map(({ id, header, sortField }) => {
                const active =
                  sortField !== undefined && sortField === appliedSort.sortBy;
                return (
                  <TableCell
                    key={id}
                    sx={{ fontWeight: "fontWeightMedium" }}
                    sortDirection={
                      active ? muiDirection[appliedSort.direction] : false
                    }
                  >
                    {sortField === undefined ? (
                      header
                    ) : (
                      <TableSortLabel
                        active={active}
                        direction={
                          active ? muiDirection[appliedSort.direction] : "asc"
                        }
                        onClick={() => sortBy(sortField)}
                      >
                        {header}
                      </TableSortLabel>
                    )}
                  </TableCell>
                );
              })}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={getRowKey(row)}>
                {columns.map(({ id, render }) => (
                  <TableCell key={id}>{render(row)}</TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination
        component="div"
        count={total}
        page={Math.floor(offset / pageSize)}
        rowsPerPage={pageSize}
        rowsPerPageOptions={rowsPerPageOptions}
        onPageChange={(_event, page) =>
          onPaginationChange({ offset: page * pageSize, pageSize })
        }
        onRowsPerPageChange={(event) =>
          onPaginationChange({
            offset: 0,
            pageSize: Number(event.target.value),
          })
        }
      />
    </Paper>
  );
}
