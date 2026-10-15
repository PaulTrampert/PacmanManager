import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type {
  Architecture,
  PublicUserInfo,
  Repository,
  RepositoryPaginatedResponse,
} from "../api";
import { apiHandler, toMockPath } from "../mocks/api";
import { anArchitecture, aPage, aRepository, aUser } from "../mocks/fixtures";
import { server } from "../mocks/node";
import { signedInHandlers } from "../mocks/session";
import { renderRoute } from "../testing/renderRoute";

const me = aUser({
  id: "11111111-1111-1111-1111-111111111111",
  displayName: "Paul Trampert",
});
const sam = aUser({
  id: "33333333-3333-3333-3333-333333333333",
  displayName: "Sam Lee",
});

const coreExtras = aRepository({
  id: "aaaaaaaa-0000-4000-8000-000000000001",
  name: "core-extras",
  owner: { id: me.id, displayName: me.displayName },
});
const games = aRepository({
  id: "aaaaaaaa-0000-4000-8000-000000000002",
  name: "games",
  owner: { id: sam.id, displayName: sam.displayName },
});
const myAurBuilds = aRepository({
  id: "aaaaaaaa-0000-4000-8000-000000000003",
  name: "my-aur-builds",
  isPublic: false,
  owner: { id: me.id, displayName: me.displayName },
});
const repositories = [coreExtras, games, myAurBuilds] as const;

interface ListPageApi {
  /** The architectures `GET /v1/Architectures` answers with; one unless given. */
  architectures?: Architecture[];
  /** How many users `GET /v1/Users` reports; three unless given. */
  userCount?: number;
  /** Answers each list request; with the three repositories unless given. */
  list?: (
    request: URLSearchParams,
  ) =>
    | RepositoryPaginatedResponse
    | Response
    | Promise<RepositoryPaginatedResponse | Response>;
}

/**
 * Answers the requests the list page makes, recording the query string of each list request.
 */
function answerListPage({
  architectures = [anArchitecture()],
  userCount = 3,
  list = () => aPage(repositories),
}: ListPageApi = {}) {
  const listRequests: URLSearchParams[] = [];
  server.use(
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json(architectures),
    ),
    apiHandler("get", "/v1/Users", () =>
      HttpResponse.json(
        aPage<PublicUserInfo>([], {
          sort: { sortBy: "DisplayName", direction: "Ascending" },
          total: userCount,
        }),
      ),
    ),
    // Untyped, so that a test can answer with any status: the document declares none but 200.
    http.get(toMockPath("/v1/Repositories"), async ({ request }) => {
      const query = new URL(request.url).searchParams;
      listRequests.push(query);
      const answer = await list(query);
      return answer instanceof Response ? answer : HttpResponse.json(answer);
    }),
  );
  return { listRequests };
}

/** `count` repositories, named `repo-000` onwards. */
function numberedRepositories(count: number): Repository[] {
  return Array.from({ length: count }, (_, index) =>
    aRepository({
      id: `bbbbbbbb-0000-4000-8000-${String(index).padStart(12, "0")}`,
      name: `repo-${String(index).padStart(3, "0")}`,
    }),
  );
}

/** Answers a list request with the page of `all` its `offset` and `pageSize` ask for. */
function pageOf(all: readonly Repository[]) {
  return (query: URLSearchParams): RepositoryPaginatedResponse => {
    const offset = Number(query.get("offset") ?? 0);
    const pageSize = Number(query.get("pageSize"));
    return aPage(all.slice(offset, offset + pageSize), {
      sort: { sortBy: "Name", direction: "Ascending" },
      offset,
      total: all.length,
    });
  };
}

function signInAs(user = me) {
  server.use(...signedInHandlers(user));
}

/**
 * How long to wait for the page to first render. Its code is split from the route tree and loaded
 * on first render, which can take over a second while other test files run.
 */
const firstRenderTimeout = 5_000;

async function findTable() {
  return screen.findByRole(
    "table",
    { name: "Repositories" },
    { timeout: firstRenderTimeout },
  );
}

/** The names in the table's rows, in order. */
function rowNames(table: HTMLElement): string[] {
  return within(table)
    .getAllByRole("row")
    .slice(1)
    .map((row) => within(row).getAllByRole("cell")[0]!.textContent);
}

describe("the repository list route", () => {
  it("renders the list page for the URL's search, with each row's link and New repository's", async () => {
    signInAs();
    const { listRequests } = answerListPage();
    const { router } = renderRoute("/repositories?nameContains=e");

    const table = await findTable();
    await waitFor(() =>
      expect(rowNames(table)).toEqual([
        "core-extras",
        "games",
        "my-aur-builds",
      ]),
    );
    expect(router.state.matches.at(-1)?.routeId).toBe("/repositories/");
    expect(
      screen.getByRole("heading", { level: 1, name: "Repositories" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "New repository" }),
    ).toHaveAttribute("href", "/repositories/new");
    expect(within(table).getByRole("link", { name: "games" })).toHaveAttribute(
      "href",
      `/repositories/${games.id}`,
    );
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("e");
    expect(listRequests.map((query) => query.toString())).toEqual([
      "nameContains=e&pageSize=50",
    ]);
  });

  it("navigates to the search the page asks for, adding a history entry", async () => {
    signInAs();
    const { listRequests } = answerListPage({
      list: pageOf(numberedRepositories(60)),
    });
    const { router } = renderRoute("/repositories?offset=50");
    const table = await findTable();
    await waitFor(() => expect(rowNames(table)).toHaveLength(10));

    await userEvent.click(
      within(table).getByRole("button", { name: "Created" }),
    );

    await waitFor(() =>
      expect(router.state.location.search).toEqual({ sortBy: "Created" }),
    );
    expect(router.history.length).toBe(2);
    await waitFor(() =>
      expect(Object.fromEntries(listRequests.at(-1)!)).toEqual({
        sortBy: "Created",
        pageSize: "50",
      }),
    );
  });

  it.each([
    {
      url: "/repositories?offset=500",
      count: 5,
      offset: 0,
      firstRow: "repo-000",
    },
    {
      url: "/repositories?offset=500&pageSize=50",
      count: 120,
      offset: 100,
      firstRow: "repo-100",
    },
  ])(
    "moves $url with $count repositories to the last page, offset=$offset, in place of the history entry",
    async ({ url, count, offset, firstRow }) => {
      signInAs();
      const { listRequests } = answerListPage({
        list: pageOf(numberedRepositories(count)),
      });
      const { router } = renderRoute(url);

      const table = await findTable();
      await waitFor(() => expect(rowNames(table)[0]).toBe(firstRow));
      expect(router.state.location.search.offset).toBe(offset);
      expect(router.history.location.search).toContain(`offset=${offset}`);
      expect(router.history.length).toBe(1);
      expect(
        screen.queryByRole("heading", { level: 2 }),
      ).not.toBeInTheDocument();
      expect(listRequests.map((query) => query.get("offset"))).toEqual([
        "500",
        String(offset),
      ]);
    },
  );

  it("drops a page size it does not offer, and asks for 50 rows", async () => {
    signInAs();
    const { listRequests } = answerListPage();
    const { router } = renderRoute("/repositories?pageSize=7&offset=0");

    await findTable();
    await waitFor(() =>
      expect(router.state.location.searchStr).toBe("?offset=0"),
    );
    expect(router.history.location.search).toBe("?offset=0");
    expect(screen.getByText("Rows per page:").parentElement).toHaveTextContent(
      "50",
    );
    expect(listRequests.map((query) => query.get("pageSize"))).toEqual(["50"]);
  });

  it.each([
    {
      param: "architecture",
      url: "/repositories?architecture=x86_64&nameContains=core",
      signedIn: true,
      api: { architectures: [anArchitecture()] },
    },
    {
      param: "ownerId",
      url: `/repositories?ownerId=${me.id}&nameContains=core`,
      signedIn: true,
      api: { userCount: 1 },
    },
    {
      param: "isPublic",
      url: "/repositories?isPublic=false&nameContains=core",
      signedIn: false,
      api: {},
    },
  ])(
    "removes $param, whose filter is disabled, by replacing the history entry",
    async ({ param, url, signedIn, api }) => {
      if (signedIn) {
        signInAs();
      }
      const { listRequests } = answerListPage(api);
      const { router } = renderRoute(url);

      await findTable();
      await waitFor(() =>
        expect(router.state.location.search).toEqual({ nameContains: "core" }),
      );
      expect(router.history.location.search).toBe("?nameContains=core");
      // Replaced, not pushed: Back never returns to the URL with the param.
      expect(router.history.length).toBe(1);
      expect(listRequests.length).toBeGreaterThan(0);
      for (const query of listRequests) {
        expect(query.has(param)).toBe(false);
      }
    },
  );
});
