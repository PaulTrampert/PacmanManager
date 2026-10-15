import {
  keepPreviousData,
  useQuery,
  useSuspenseQuery,
} from "@tanstack/react-query";
import { useEffect, useState } from "react";
import {
  ApiError,
  type Architecture,
  type PublicUserInfo,
  type RepositoryPaginatedResponse,
  type RepositorySortField,
} from "../../api";
import type {
  Pagination,
  RequestedSort,
} from "../../components/PaginatedTable";
import type {
  OwnerSearch,
  RepositoryFilterChange,
} from "../../components/RepositoryFilters";
import { architecturesQuery } from "../../queries/architectures";
import { repositoryListQuery } from "../../queries/repositories";
import {
  userListQuery,
  userQuery,
  type UserListSearch,
} from "../../queries/users";
import {
  defaultRepositoryListPageSize,
  hasRepositoryListFilters,
  lastPageOffset,
  repositoryListRequest,
  userCountSearch,
  withFilterChange,
  withoutFilters,
  withPagination,
  withSort,
  type RepositoryListPageSearch,
} from "../../repositoryListSearch";
import { useSession, type Session } from "../../session";

/** How many users the owner filter lists for one search. */
export const ownerSearchPageSize = 20;

/**
 * Moves the list to the search params `update` makes of the current ones; with `replace`, in place
 * of the current history entry. It takes an update rather than the new params so that a change
 * reported late, as the name search reports 300 ms after typing stops, applies to the params as they
 * are by then rather than undoing a change made meanwhile.
 */
export type RepositoryListSearchChange = (
  update: (previous: RepositoryListPageSearch) => RepositoryListPageSearch,
  options?: { replace?: boolean },
) => void;

/** What {@link useRepositoryListPage} needs from `RepositoryListPage`'s props. */
export interface UseRepositoryListPageOptions {
  /** The route's validated search params. */
  search: RepositoryListPageSearch;
  /** Moves the list to other search params. */
  onSearchChange: RepositoryListSearchChange;
}

/**
 * Which empty state the list shows, when it is empty: no repositories match the filters, a
 * signed-in user has none yet, or there are no public ones for an anonymous visitor.
 */
export type RepositoryListEmptyState = "noMatches" | "noneYet" | "noPublic";

/** What the repository list page renders, as {@link useRepositoryListPage} works it out. */
export interface RepositoryListPageState {
  /** Who is using the app. */
  session: Session;
  /** The architectures a repository may support. */
  architectures: Architecture[];
  /** The filter bar. */
  filters: {
    /** Applies one filter's change, starting again at the first page. */
    onChange: (change: RepositoryFilterChange) => void;
    /** The users the owner filter offers. */
    users: OwnerSearch;
    /** The user `ownerId` names, once fetched; never the signed-in user. */
    owner: PublicUserInfo | undefined;
    /** Removes every filter: **Clear filters**. */
    clear: () => void;
  };
  /** The list itself. */
  list: {
    /**
     * The page to show: the latest one within the listing, kept while the next loads. Never a page
     * past the end; absent until the first page within the listing arrives.
     */
    response: RepositoryPaginatedResponse | undefined;
    /** Whether a page is on its way, so the one shown is dimmed under a spinner. */
    loading: boolean;
    /**
     * The empty state to show in place of the table, when the latest response, not a placeholder,
     * has a `total` of 0; otherwise absent.
     */
    emptyState: RepositoryListEmptyState | undefined;
    /** Why the list could not be fetched, if it could not. */
    error: Error | null;
    /** Fetches the list again. */
    retry: () => void;
    /** The number of rows a page holds. */
    pageSize: number;
    /** Applies a sort asked for from a column header, starting again at the first page. */
    onSortChange: (sort: RequestedSort<RepositorySortField>) => void;
    /** Moves to the page asked for. */
    onPaginationChange: (pagination: Pagination) => void;
  };
}

/** Whether `response` is a page past the end: it has no results, but the listing has some. */
function isPastTheEnd(response: RepositoryPaginatedResponse | undefined) {
  return (
    response !== undefined &&
    (response.results ?? []).length === 0 &&
    (response.total ?? 0) > 0
  );
}

/** The users search for the text typed into the owner filter. */
function ownerSearch(text: string): UserListSearch {
  return text === ""
    ? { pageSize: ownerSearchPageSize }
    : { displayNameContains: text, pageSize: ownerSearchPageSize };
}

/**
 * The behaviour of the repository list page: the list and the filters' data, read for the route's
 * search params, and every filter, sort and page change written back to them.
 *
 * A filter, sort or page-size change removes `offset`. The owner filter searches the users as typed
 * and labels an `ownerId` that is not the signed-in user's with that user's name; one naming no user
 * is removed. A page past the end is replaced by the last page, and is never what the table shows.
 */
export function useRepositoryListPage({
  search,
  onSearchChange,
}: UseRepositoryListPageOptions): RepositoryListPageState {
  const session = useSession();
  // Both are ensured by the route's beforeLoad, so neither suspends.
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  const { data: users } = useSuspenseQuery(userListQuery(userCountSearch));
  const userCount = users.total ?? 0;

  const pageSize = search.pageSize ?? defaultRepositoryListPageSize;
  const list = useQuery({
    ...repositoryListQuery(repositoryListRequest(search)),
    placeholderData: keepPreviousData,
  });

  // The last page within the listing, shown in place of a page past the end until the last page
  // arrives.
  const pastTheEnd = isPastTheEnd(list.data);
  const [shown, setShown] = useState(pastTheEnd ? undefined : list.data);
  if (!pastTheEnd && list.data !== shown) {
    setShown(list.data);
  }

  const total = list.data?.total ?? 0;
  const replaceWithLastPage = pastTheEnd && !list.isPlaceholderData;
  useEffect(() => {
    if (replaceWithLastPage) {
      onSearchChange(
        (previous) => ({
          ...previous,
          offset: lastPageOffset(total, pageSize),
        }),
        { replace: true },
      );
    }
  }, [replaceWithLastPage, total, pageSize, onSearchChange]);

  const [ownerText, setOwnerText] = useState("");
  const ownerResults = useQuery({
    ...userListQuery(ownerSearch(ownerText)),
    enabled: userCount > 1,
    placeholderData: keepPreviousData,
  });

  const ownerId = search.ownerId;
  const labelsOwner =
    ownerId !== undefined &&
    !(session.status === "signedIn" && session.user.id === ownerId);
  const owner = useQuery({
    ...userQuery(ownerId ?? ""),
    enabled: labelsOwner,
  });
  const ownerMissing =
    labelsOwner &&
    owner.error instanceof ApiError &&
    owner.error.status === 404;
  useEffect(() => {
    if (ownerMissing) {
      onSearchChange(
        (previous) => withFilterChange(previous, { ownerId: undefined }),
        {
          replace: true,
        },
      );
    }
  }, [ownerMissing, onSearchChange]);

  const empty = !list.isPlaceholderData && list.data?.total === 0;
  let emptyState: RepositoryListEmptyState | undefined;
  if (empty) {
    emptyState = hasRepositoryListFilters(search)
      ? "noMatches"
      : session.status === "signedIn"
        ? "noneYet"
        : "noPublic";
  }

  return {
    session,
    architectures,
    filters: {
      onChange: (change) =>
        onSearchChange((previous) => withFilterChange(previous, change)),
      users: {
        results: ownerResults.data?.results ?? [],
        total: userCount,
        loading: ownerResults.isFetching,
        onSearch: setOwnerText,
      },
      owner: labelsOwner ? owner.data : undefined,
      clear: () => onSearchChange(withoutFilters),
    },
    list: {
      response: shown,
      loading: list.isFetching || pastTheEnd,
      emptyState,
      error: list.error,
      retry: () => void list.refetch(),
      pageSize,
      onSortChange: (sort) =>
        onSearchChange((previous) => withSort(previous, sort)),
      onPaginationChange: (pagination) =>
        onSearchChange((previous) => withPagination(previous, pagination)),
    },
  };
}
