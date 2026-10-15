import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import { createLink, type LinkOptions } from "@tanstack/react-router";
import type { Repository } from "../../api";
import { EmptyState } from "../../components/EmptyState";
import { ErrorPage } from "../../components/ErrorPage";
import { PageHeader } from "../../components/PageHeader";
import { RepositoryFilters } from "../../components/RepositoryFilters";
import { RepositoryTable } from "../../components/RepositoryTable";
import type { RepositoryListPageSearch } from "../../repositoryListSearch";
import {
  useRepositoryListPage,
  type RepositoryListEmptyState,
  type RepositoryListSearchChange,
} from "./useRepositoryListPage";

export type { RepositoryListSearchChange };

/** MUI's button, navigating through the router. */
const ButtonLink = createLink(Button);

/** The props {@link RepositoryListPage} takes. */
export interface RepositoryListPageProps {
  /** The route's validated search params: the filters, the sort and the page. */
  search: RepositoryListPageSearch;
  /** Where a row's name links to: the repository's page. */
  repositoryLink: (repository: Repository) => LinkOptions;
  /** Where **New repository** and **Create a repository** link to. */
  newRepositoryLink: LinkOptions;
  /** Moves the list to other search params; every filter, sort and page change calls it. */
  onSearchChange: RepositoryListSearchChange;
}

/**
 * The repository list: a page header with **New repository** for a signed-in user, the filter bar,
 * and one page of the repositories in a table, or the empty state that suits the list. Every filter,
 * sort and page change is reported through `onSearchChange`.
 */
export function RepositoryListPage({
  search,
  repositoryLink,
  newRepositoryLink,
  onSearchChange,
}: RepositoryListPageProps) {
  const { session, architectures, filters, list } = useRepositoryListPage({
    search,
    onSearchChange,
  });

  return (
    <>
      <PageHeader
        title="Repositories"
        actions={
          session.status === "signedIn" && (
            <ButtonLink {...newRepositoryLink} variant="contained">
              New repository
            </ButtonLink>
          )
        }
      />
      <Stack spacing={3}>
        <RepositoryFilters
          filters={search}
          onChange={filters.onChange}
          session={session}
          architectures={architectures}
          users={filters.users}
          owner={filters.owner}
        />
        {list.error !== null ? (
          <ErrorPage error={list.error} onRetry={list.retry} />
        ) : list.emptyState !== undefined ? (
          <ListEmptyState
            emptyState={list.emptyState}
            newRepositoryLink={newRepositoryLink}
            onClearFilters={filters.clear}
          />
        ) : (
          <RepositoryTable
            response={list.response}
            loading={list.loading}
            pageSize={list.pageSize}
            architectures={architectures}
            repositoryLink={repositoryLink}
            onSortChange={list.onSortChange}
            onPaginationChange={list.onPaginationChange}
          />
        )}
      </Stack>
    </>
  );
}

function ListEmptyState({
  emptyState,
  newRepositoryLink,
  onClearFilters,
}: {
  emptyState: RepositoryListEmptyState;
  newRepositoryLink: LinkOptions;
  onClearFilters: () => void;
}) {
  switch (emptyState) {
    case "noMatches":
      return (
        <EmptyState
          heading="No repositories match"
          message="Nothing matches the current filters."
          action={
            <Button variant="outlined" onClick={onClearFilters}>
              Clear filters
            </Button>
          }
        />
      );
    case "noneYet":
      return (
        <EmptyState
          heading="No repositories yet"
          message="Create a repository to start publishing packages."
          action={
            <ButtonLink {...newRepositoryLink} variant="contained">
              Create a repository
            </ButtonLink>
          }
        />
      );
    case "noPublic":
      return (
        <EmptyState
          heading="No public repositories"
          message="There are no public repositories to show."
        />
      );
  }
}
