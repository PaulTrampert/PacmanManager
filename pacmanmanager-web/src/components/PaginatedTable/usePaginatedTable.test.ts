import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { AppliedSort } from "./PaginatedTable";
import { usePaginatedTable } from "./usePaginatedTable";

type Field = "Name" | "CreatedAt";

function setup({
  appliedSort = { sortBy: "Name", direction: "Ascending" },
  noAppliedSort = false,
  offset = 0,
  pageSize = 25,
}: {
  appliedSort?: AppliedSort<Field>;
  /** Leaves the applied sort absent, as before the first response. */
  noAppliedSort?: boolean;
  offset?: number;
  pageSize?: number;
} = {}) {
  const onSortChange = vi.fn();
  const onPaginationChange = vi.fn();
  const { result } = renderHook(() =>
    usePaginatedTable<Field>({
      appliedSort: noAppliedSort ? undefined : appliedSort,
      onSortChange,
      offset,
      pageSize,
      onPaginationChange,
    }),
  );
  return { result, onSortChange, onPaginationChange };
}

describe("usePaginatedTable", () => {
  describe("sortBy", () => {
    it("reverses an ascending sort when the active header is clicked", () => {
      const { result, onSortChange } = setup({
        appliedSort: { sortBy: "Name", direction: "Ascending" },
      });

      result.current.sortBy("Name");

      expect(onSortChange).toHaveBeenCalledExactlyOnceWith({
        sortBy: "Name",
        direction: "Descending",
      });
    });

    it("reverses a descending sort when the active header is clicked", () => {
      const { result, onSortChange } = setup({
        appliedSort: { sortBy: "Name", direction: "Descending" },
      });

      result.current.sortBy("Name");

      expect(onSortChange).toHaveBeenCalledExactlyOnceWith({
        sortBy: "Name",
        direction: "Ascending",
      });
    });

    it("asks for another field with no direction key", () => {
      const { result, onSortChange } = setup({
        appliedSort: { sortBy: "Name", direction: "Descending" },
      });

      result.current.sortBy("CreatedAt");

      expect(onSortChange).toHaveBeenCalledOnce();
      const [sort] = onSortChange.mock.calls[0] as [object];
      expect(sort).toStrictEqual({ sortBy: "CreatedAt" });
      expect("direction" in sort).toBe(false);
    });
  });

  describe("with no applied sort", () => {
    it("asks for the clicked field with no direction key", () => {
      const { result, onSortChange } = setup({ noAppliedSort: true });

      result.current.sortBy("Name");

      expect(onSortChange).toHaveBeenCalledOnce();
      expect(onSortChange.mock.calls[0][0]).toStrictEqual({ sortBy: "Name" });
    });

    it("leaves every column inactive", () => {
      const { result } = setup({ noAppliedSort: true });

      expect(result.current.headerSort("Name")).toEqual({
        active: false,
        direction: "asc",
        sortDirection: false,
      });
    });
  });

  describe("headerSort", () => {
    it("marks the active column with the applied direction, ascending", () => {
      const { result } = setup({
        appliedSort: { sortBy: "Name", direction: "Ascending" },
      });

      expect(result.current.headerSort("Name")).toEqual({
        active: true,
        direction: "asc",
        sortDirection: "asc",
      });
    });

    it("marks the active column with the applied direction, descending", () => {
      const { result } = setup({
        appliedSort: { sortBy: "Name", direction: "Descending" },
      });

      expect(result.current.headerSort("Name")).toEqual({
        active: true,
        direction: "desc",
        sortDirection: "desc",
      });
    });

    it("leaves another sortable column inactive, pointing ascending", () => {
      const { result } = setup({
        appliedSort: { sortBy: "Name", direction: "Descending" },
      });

      expect(result.current.headerSort("CreatedAt")).toEqual({
        active: false,
        direction: "asc",
        sortDirection: false,
      });
    });

    it("leaves a column with no sort field inactive", () => {
      const { result } = setup();

      expect(result.current.headerSort(undefined)).toEqual({
        active: false,
        direction: "asc",
        sortDirection: false,
      });
    });
  });

  describe("page", () => {
    it("is the offset divided by the page size", () => {
      expect(setup({ offset: 50, pageSize: 25 }).result.current.page).toBe(2);
    });

    it("rounds an offset that is not on a page boundary down", () => {
      expect(setup({ offset: 60, pageSize: 25 }).result.current.page).toBe(2);
    });

    it("is 0 at offset 0", () => {
      expect(setup({ offset: 0, pageSize: 50 }).result.current.page).toBe(0);
    });
  });

  describe("changePage", () => {
    it("asks for the page's offset at the same page size", () => {
      const { result, onPaginationChange } = setup({
        offset: 0,
        pageSize: 25,
      });

      result.current.changePage(3);

      expect(onPaginationChange).toHaveBeenCalledExactlyOnceWith({
        offset: 75,
        pageSize: 25,
      });
    });
  });

  describe("changePageSize", () => {
    it("asks for the new page size at offset 0, in one call", () => {
      const { result, onPaginationChange } = setup({
        offset: 75,
        pageSize: 25,
      });

      result.current.changePageSize("50");

      expect(onPaginationChange).toHaveBeenCalledExactlyOnceWith({
        offset: 0,
        pageSize: 50,
      });
    });

    it("takes the page size as a number too", () => {
      const { result, onPaginationChange } = setup({ offset: 50 });

      result.current.changePageSize(100);

      expect(onPaginationChange).toHaveBeenCalledExactlyOnceWith({
        offset: 0,
        pageSize: 100,
      });
    });
  });
});
