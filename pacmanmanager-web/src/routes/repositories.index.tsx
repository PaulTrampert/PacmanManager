import { createFileRoute, linkOptions, redirect } from "@tanstack/react-router";
import { RepositoryListPage } from "../pages/RepositoryListPage";
import { architecturesQuery } from "../queries/architectures";
import { repositoryListQuery } from "../queries/repositories";
import { userListQuery } from "../queries/users";
import {
  hasDroppedParams,
  repositoryListRequest,
  userCountSearch,
  validateRepositoryListSearch,
  withoutDisabledFilters,
} from "../repositoryListSearch";

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
  component: RepositoryListRoute,
});

function RepositoryListRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return (
    <RepositoryListPage
      search={search}
      repositoryLink={(repository) =>
        linkOptions({
          to: "/repositories/$repositoryId",
          params: { repositoryId: repository.id },
        })
      }
      newRepositoryLink={linkOptions({ to: "/repositories/new" })}
      onSearchChange={(update, options) =>
        void navigate({ search: update, replace: options?.replace })
      }
    />
  );
}
