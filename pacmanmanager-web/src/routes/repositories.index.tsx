import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import {
  createFileRoute,
  createLink,
  linkOptions,
  redirect,
} from "@tanstack/react-router";
import { EmptyState } from "../components/EmptyState";
import { ErrorPage } from "../components/ErrorPage";
import { PageHeader } from "../components/PageHeader";
import { RepositoryFilters } from "../components/RepositoryFilters";
import { RepositoryTable } from "../components/RepositoryTable";
import { architecturesQuery } from "../queries/architectures";
import { repositoryListQuery } from "../queries/repositories";
import { userListQuery } from "../queries/users";
import {
  hasDroppedParams,
  hasRepositoryListFilters,
  repositoryListRequest,
  userCountSearch,
  validateRepositoryListSearch,
  withoutDisabledFilters,
  type RepositoryListPageSearch,
} from "../repositoryListSearch";
import { useSession, type Session } from "../session";
import { useRepositoryListPage } from "./-useRepositoryListPage";

/** MUI's button, navigating through the router. */
const ButtonLink = createLink(Button);

export const Route = createFileRoute("/repositories/")({
  validateSearch: validateRepositoryListSearch,
  // No loader, and no loaderDeps for the list's params: changing a filter, the sort or the page
  // never shows the pending component, so the page and the name being typed stay mounted.
  beforeLoad: async ({ context: { queryClient, session }, search }) => {
    // The router keeps the params validateSearch dropped in the search it passes on, since the
    // root route takes every param as it is; validating again leaves only the list's own.
    const valid = validateRepositoryListSearch(search);
    const [architectures, users] = await Promise.all([
      queryClient.ensureQueryData(architecturesQuery()),
      queryClient.ensureQueryData(userListQuery(userCountSearch)),
    ]);
    const applicable = withoutDisabledFilters(valid, {
      session,
      architectures,
      userCount: users.total ?? 0,
    });
    // The URL never shows a param the page ignores: one validateSearch dropped, or a disabled
    // filter's. Replacing the entry means Back never returns to it.
    if (applicable !== valid || hasDroppedParams(search, valid)) {
      throw redirect({
        to: "/repositories",
        search: applicable,
        replace: true,
      });
    }
    void queryClient.prefetchQuery(
      repositoryListQuery(repositoryListRequest(valid)),
    );
  },
  component: RepositoryListPage,
});

function RepositoryListPage() {
  const search = Route.useSearch();
  const session = useSession();
  const routeNavigate = Route.useNavigate();
  const { architectures, filters, list } = useRepositoryListPage({
    search,
    session,
    navigate: (update, options) =>
      void routeNavigate({ search: update, replace: options?.replace }),
  });

  return (
    <>
      <PageHeader
        title="Repositories"
        actions={
          session.status === "signedIn" && (
            <ButtonLink to="/repositories/new" variant="contained">
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
        ) : list.empty ? (
          <RepositoryListEmptyState
            search={search}
            session={session}
            onClearFilters={filters.clear}
          />
        ) : (
          <RepositoryTable
            response={list.response}
            loading={list.loading}
            pageSize={list.pageSize}
            architectures={architectures}
            repositoryLink={(repository) =>
              linkOptions({
                to: "/repositories/$repositoryId",
                params: { repositoryId: repository.id },
              })
            }
            onSortChange={list.onSortChange}
            onPaginationChange={list.onPaginationChange}
          />
        )}
      </Stack>
    </>
  );
}

function RepositoryListEmptyState({
  search,
  session,
  onClearFilters,
}: {
  search: RepositoryListPageSearch;
  session: Session;
  onClearFilters: () => void;
}) {
  if (hasRepositoryListFilters(search)) {
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
  }
  if (session.status === "signedIn") {
    return (
      <EmptyState
        heading="No repositories yet"
        message="Create a repository to start publishing packages."
        action={
          <ButtonLink to="/repositories/new" variant="contained">
            Create a repository
          </ButtonLink>
        }
      />
    );
  }
  return (
    <EmptyState
      heading="No public repositories"
      message="There are no public repositories to show."
    />
  );
}
