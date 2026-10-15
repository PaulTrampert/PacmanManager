import { QueryClientProvider, type QueryClient } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import type {
  PublicUserInfo,
  Repository,
  RepositoryPaginatedResponse,
} from "../../api";
import { apiHandler } from "../../mocks/api";
import {
  anArchitecture,
  aPage,
  aRepository,
  aUser,
} from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { architecturesQuery } from "../../queries/architectures";
import { userListQuery } from "../../queries/users";
import { createQueryClient } from "../../queryClient";
import {
  userCountSearch,
  type RepositoryListPageSearch,
} from "../../repositoryListSearch";
import { sessionQuery, type Session } from "../../session";
import {
  useRepositoryListPage,
  type RepositoryListSearchChange,
} from "./useRepositoryListPage";

const me = aUser({ id: "11111111-1111-1111-1111-111111111111" });
const sam = aUser({
  id: "33333333-3333-3333-3333-333333333333",
  displayName: "Sam Lee",
});
const signedIn: Session = { status: "signedIn", user: me };
const anonymous: Session = { status: "anonymous" };

/**
 * A query client holding what the routes ensure before the page renders: the session, and the
 * architectures and user count.
 */
function seededQueryClient(session: Session, userCount: number): QueryClient {
  const queryClient = createQueryClient();
  queryClient.setQueryData(sessionQuery().queryKey, session);
  queryClient.setQueryData(architecturesQuery().queryKey, [anArchitecture()]);
  queryClient.setQueryData(
    userListQuery(userCountSearch).queryKey,
    aPage<PublicUserInfo>([], {
      sort: { sortBy: "DisplayName", direction: "Ascending" },
      total: userCount,
    }),
  );
  return queryClient;
}

/** Answers the list with `answer`, and the owner search with no users. */
function answerList(
  answer: (query: URLSearchParams) => RepositoryPaginatedResponse = () =>
    aPage([aRepository()]),
) {
  server.use(
    apiHandler("get", "/v1/Repositories", ({ request }) =>
      HttpResponse.json(answer(new URL(request.url).searchParams)),
    ),
    apiHandler("get", "/v1/Users", () =>
      HttpResponse.json(
        aPage<PublicUserInfo>([], {
          sort: { sortBy: "DisplayName", direction: "Ascending" },
        }),
      ),
    ),
  );
}

function renderPage(
  search: RepositoryListPageSearch,
  { session = signedIn, userCount = 3 } = {},
) {
  const navigate = vi.fn<RepositoryListSearchChange>();
  const queryClient = seededQueryClient(session, userCount);
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const hook = renderHook(
    () => useRepositoryListPage({ search, onSearchChange: navigate }),
    { wrapper },
  );
  /** What the `index`th navigation made of `previous`, and whether it replaced the entry. */
  const navigation = (index: number, previous: RepositoryListPageSearch) => {
    const [update, options] = navigate.mock.calls[index]!;
    return { search: update(previous), replace: options?.replace ?? false };
  };
  return { ...hook, navigate, navigation };
}

describe("useRepositoryListPage", () => {
  it("applies a filter change and starts again at the first page", async () => {
    answerList();
    const { result, navigation } = renderPage({});
    await waitFor(() => expect(result.current.list.response).toBeDefined());

    act(() => result.current.filters.onChange({ isPublic: false }));

    expect(
      navigation(0, { nameContains: "core", offset: 50, sortBy: "Created" }),
    ).toEqual({
      search: { nameContains: "core", isPublic: false, sortBy: "Created" },
      replace: false,
    });
  });

  it("clears every filter, keeping the sort and page size", async () => {
    answerList();
    const { result, navigation } = renderPage({});
    await waitFor(() => expect(result.current.list.response).toBeDefined());

    act(() => result.current.filters.clear());

    expect(
      navigation(0, {
        nameContains: "core",
        isPublic: true,
        ownerId: me.id,
        architecture: "x86_64",
        sortBy: "Updated",
        offset: 25,
        pageSize: 25,
      }).search,
    ).toEqual({ sortBy: "Updated", pageSize: 25 });
  });

  it("applies a sort and starts again at the first page", async () => {
    answerList();
    const { result, navigation } = renderPage({});
    await waitFor(() => expect(result.current.list.response).toBeDefined());

    act(() => result.current.list.onSortChange({ sortBy: "Created" }));

    expect(
      navigation(0, { sortBy: "Name", direction: "Descending", offset: 25 })
        .search,
    ).toEqual({ sortBy: "Created" });
  });

  it("moves to the page asked for", async () => {
    answerList();
    const { result, navigation } = renderPage({});
    await waitFor(() => expect(result.current.list.response).toBeDefined());

    act(() =>
      result.current.list.onPaginationChange({ offset: 25, pageSize: 25 }),
    );

    expect(navigation(0, { nameContains: "core" }).search).toEqual({
      nameContains: "core",
      offset: 25,
      pageSize: 25,
    });
  });

  it("never shows a page past the end, and replaces it with the last page", async () => {
    answerList(() =>
      aPage<Repository>([], {
        sort: { sortBy: "Name", direction: "Ascending" },
        offset: 500,
        total: 120,
      }),
    );
    const { result, navigate, navigation } = renderPage({ offset: 500 });

    await waitFor(() => expect(navigate).toHaveBeenCalledTimes(1));
    expect(navigation(0, { offset: 500, nameContains: "core" })).toEqual({
      search: { offset: 100, nameContains: "core" },
      replace: true,
    });
    expect(result.current.list.response).toBeUndefined();
    expect(result.current.list.loading).toBe(true);
    expect(result.current.list.emptyState).toBeUndefined();
  });

  it.each([
    {
      search: { nameContains: "x" },
      session: signedIn,
      emptyState: "noMatches",
    },
    { search: {}, session: signedIn, emptyState: "noneYet" },
    { search: {}, session: anonymous, emptyState: "noPublic" },
  ] as const)(
    "chooses the $emptyState empty state when the listing's total is 0",
    async ({ search, session, emptyState }) => {
      answerList(() =>
        aPage<Repository>([], {
          sort: { sortBy: "Name", direction: "Ascending" },
        }),
      );
      const { result, navigate } = renderPage(search, { session });

      await waitFor(() =>
        expect(result.current.list.emptyState).toBe(emptyState),
      );
      expect(navigate).not.toHaveBeenCalled();
    },
  );

  it("searches the owners with the text typed, 20 at a time", async () => {
    const searches: URLSearchParams[] = [];
    answerList();
    server.use(
      apiHandler("get", "/v1/Users", ({ request }) => {
        searches.push(new URL(request.url).searchParams);
        return HttpResponse.json(aPage([sam]));
      }),
    );
    const { result } = renderPage({});
    await waitFor(() =>
      expect(result.current.filters.users.results).toEqual([sam]),
    );

    act(() => result.current.filters.users.onSearch("sam"));

    await waitFor(() => expect(searches).toHaveLength(2));
    expect(Object.fromEntries(searches[0]!)).toEqual({ pageSize: "20" });
    expect(Object.fromEntries(searches[1]!)).toEqual({
      displayNameContains: "sam",
      pageSize: "20",
    });
    expect(result.current.filters.users.total).toBe(3);
  });

  it("does not search the owners when there is one user", async () => {
    // No handler answers GET /v1/Users, so a search would fail the test.
    server.use(
      apiHandler("get", "/v1/Repositories", () =>
        HttpResponse.json(aPage([aRepository()])),
      ),
    );
    const { result } = renderPage({}, { userCount: 1 });
    await waitFor(() => expect(result.current.list.response).toBeDefined());

    expect(result.current.filters.users.results).toEqual([]);
    expect(result.current.filters.users.total).toBe(1);
  });

  it("labels another user's ownerId with their record", async () => {
    answerList();
    server.use(
      apiHandler("get", "/v1/Users/{userId}", ({ params }) =>
        HttpResponse.json({ ...sam, id: params.userId }),
      ),
    );
    const { result } = renderPage({ ownerId: sam.id });

    await waitFor(() => expect(result.current.filters.owner).toEqual(sam));
  });

  it("does not fetch the signed-in user's own record for their ownerId", async () => {
    // No handler answers GET /v1/Users/{userId}, so fetching it would fail the test.
    answerList();
    const { result } = renderPage({ ownerId: me.id });
    await waitFor(() => expect(result.current.list.response).toBeDefined());

    expect(result.current.filters.owner).toBeUndefined();
  });

  it("removes an ownerId naming no user, in place of the history entry", async () => {
    answerList();
    server.use(
      apiHandler("get", "/v1/Users/{userId}", () =>
        HttpResponse.json({ title: "Not Found", status: 404 }, { status: 404 }),
      ),
    );
    const { navigate, navigation } = renderPage({ ownerId: sam.id });

    await waitFor(() => expect(navigate).toHaveBeenCalledTimes(1));
    expect(navigation(0, { ownerId: sam.id, nameContains: "core" })).toEqual({
      search: { nameContains: "core" },
      replace: true,
    });
  });
});
