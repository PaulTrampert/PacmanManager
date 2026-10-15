import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import {
  keepPreviousData,
  useQuery,
  useSuspenseQuery,
} from "@tanstack/react-query";
import {
  createFileRoute,
  createLink,
  linkOptions,
  redirect,
} from "@tanstack/react-router";
import type { RepositoryPaginatedResponse } from "../api";
import { EmptyState } from "../components/EmptyState";
import { ErrorPage } from "../components/ErrorPage";
import { PageHeader } from "../components/PageHeader";
import { RepositoryFilters } from "../components/RepositoryFilters";
import { RepositoryTable } from "../components/RepositoryTable";
import { architecturesQuery } from "../queries/architectures";
import { repositoryListQuery } from "../queries/repositories";
import { userListQuery } from "../queries/users";
import {
  defaultRepositoryListPageSize,
  hasDroppedParams,
  hasRepositoryListFilters,
  repositoryListRequest,
  userCountSearch,
  validateRepositoryListSearch,
  withoutDisabledFilters,
  type RepositoryListPageSearch,
} from "../repositoryListSearch";
import { useSession, type Session } from "../session";

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

// The filters, the sort and the page are connected to the URL by step 36 of the Web UI:
// Repositories plan; until then their callbacks do nothing.
function notYetConnected() {}

function RepositoryListPage() {
  const search = Route.useSearch();
  const session = useSession();
  // Both are ensured by beforeLoad, so neither suspends.
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  const { data: users } = useSuspenseQuery(userListQuery(userCountSearch));
  const list = useQuery({
    ...repositoryListQuery(repositoryListRequest(search)),
    placeholderData: keepPreviousData,
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
          onChange={notYetConnected}
          session={session}
          architectures={architectures}
          users={{ results: [], total: users.total, onSearch: notYetConnected }}
        />
        {list.isError ? (
          <ErrorPage error={list.error} onRetry={() => void list.refetch()} />
        ) : isEmpty(list.data, list.isPlaceholderData) ? (
          <RepositoryListEmptyState search={search} session={session} />
        ) : (
          <RepositoryTable
            response={list.data}
            loading={list.isFetching}
            pageSize={search.pageSize ?? defaultRepositoryListPageSize}
            architectures={architectures}
            repositoryLink={(repository) =>
              linkOptions({
                to: "/repositories/$repositoryId",
                params: { repositoryId: repository.id },
              })
            }
            onSortChange={notYetConnected}
            onPaginationChange={notYetConnected}
          />
        )}
      </Stack>
    </>
  );
}

/**
 * Whether the list is empty: the current response has a `total` of 0. A page past the end, empty
 * but with a `total`, is not, and neither is the previous response shown while the next loads.
 */
function isEmpty(
  response: RepositoryPaginatedResponse | undefined,
  isPlaceholderData: boolean,
): boolean {
  return !isPlaceholderData && response?.total === 0;
}

function RepositoryListEmptyState({
  search,
  session,
}: {
  search: RepositoryListPageSearch;
  session: Session;
}) {
  if (hasRepositoryListFilters(search)) {
    return (
      <EmptyState
        heading="No repositories match"
        message="Nothing matches the current filters."
        action={
          <Button variant="outlined" onClick={notYetConnected}>
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
