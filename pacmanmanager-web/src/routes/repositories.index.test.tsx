import { act, screen, waitFor, within } from "@testing-library/react";
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

const noRepositories: RepositoryPaginatedResponse = aPage<Repository>([], {
  sort: { sortBy: "Name", direction: "Ascending" },
});

interface ListPageApi {
  /** The architectures `GET /v1/Architectures` answers with; one unless given. */
  architectures?: Architecture[];
  /** How many users `GET /v1/Users` reports; three unless given. */
  userCount?: number;
  /** The users the owner search finds; none unless given. */
  users?: PublicUserInfo[];
  /** The users `GET /v1/Users/{userId}` knows; any other is a `404`. */
  knownUsers?: PublicUserInfo[];
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
  users = [],
  knownUsers = [],
  list = () => aPage(repositories),
}: ListPageApi = {}) {
  const listRequests: URLSearchParams[] = [];
  const userSearches: URLSearchParams[] = [];
  server.use(
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json(architectures),
    ),
    apiHandler("get", "/v1/Users", ({ request }) => {
      const query = new URL(request.url).searchParams;
      if (query.get("pageSize") !== "1") {
        userSearches.push(query);
      }
      return HttpResponse.json(
        aPage<PublicUserInfo>(query.get("pageSize") === "1" ? [] : users, {
          sort: { sortBy: "DisplayName", direction: "Ascending" },
          total: userCount,
        }),
      );
    }),
    apiHandler("get", "/v1/Users/{userId}", ({ params }) => {
      if (params.userId === "me") {
        // The session's own record, which signedInHandlers answers.
        return undefined;
      }
      const user = knownUsers.find(({ id }) => id === params.userId);
      return user === undefined
        ? HttpResponse.json(
            { title: "Not Found", status: 404 },
            { status: 404 },
          )
        : HttpResponse.json(user);
    }),
    // Untyped, so that a test can answer with any status: the document declares none but 200.
    http.get(toMockPath("/v1/Repositories"), async ({ request }) => {
      const query = new URL(request.url).searchParams;
      listRequests.push(query);
      const answer = await list(query);
      return answer instanceof Response ? answer : HttpResponse.json(answer);
    }),
  );
  return { listRequests, userSearches };
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
  it("shows a signed-in user the repositories, New repository and every filter (list-signed-in)", async () => {
    signInAs();
    const { listRequests } = answerListPage();
    const { router } = renderRoute("/repositories");

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
    expect(within(table).getAllByText("Paul Trampert")).toHaveLength(2);
    expect(within(table).getByText("Private")).toBeInTheDocument();

    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("");
    const visibility = screen.getByRole("combobox", { name: "Visibility" });
    expect(visibility).toHaveTextContent("All");
    expect(visibility).not.toHaveAttribute("aria-disabled");
    expect(screen.getByRole("combobox", { name: "Owner" })).toBeEnabled();
    expect(
      screen.getByRole("combobox", { name: "Architecture" }),
    ).toHaveAttribute("aria-disabled", "true");

    expect(listRequests.map((query) => query.toString())).toEqual([
      "pageSize=50",
    ]);
  });

  it("shows an anonymous visitor no New repository, and visibility fixed on Public (list-anonymous)", async () => {
    const { listRequests } = answerListPage({
      list: () => aPage([coreExtras, games]),
    });
    renderRoute("/repositories");

    const table = await findTable();
    await waitFor(() =>
      expect(rowNames(table)).toEqual(["core-extras", "games"]),
    );
    expect(screen.getByRole("link", { name: "Sign in" })).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "New repository" }),
    ).not.toBeInTheDocument();
    const visibility = screen.getByRole("combobox", { name: "Visibility" });
    expect(visibility).toHaveAttribute("aria-disabled", "true");
    expect(visibility).toHaveTextContent("Public");
    expect(listRequests.map((query) => query.toString())).toEqual([
      "pageSize=50",
    ]);
  });

  it("disables the owner filter on All when there is one user (list-single-user)", async () => {
    signInAs();
    answerListPage({ userCount: 1, list: () => aPage([coreExtras]) });
    renderRoute("/repositories");

    await findTable();
    const owner = screen.getByRole("combobox", { name: "Owner" });
    expect(owner).toBeDisabled();
    expect(owner).toHaveAttribute("placeholder", "All");
  });

  it("offers a signed-in user with no repositories to create one (list-empty-signed-in)", async () => {
    signInAs();
    answerListPage({ list: () => noRepositories });
    renderRoute("/repositories");

    expect(
      await screen.findByRole(
        "heading",
        {
          level: 2,
          name: "No repositories yet",
        },
        { timeout: firstRenderTimeout },
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Create a repository to start publishing packages."),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Create a repository" }),
    ).toHaveAttribute("href", "/repositories/new");
    expect(
      screen.queryByRole("table", { name: "Repositories" }),
    ).not.toBeInTheDocument();
  });

  it("offers to clear the filters when none match (list-empty-filtered)", async () => {
    signInAs();
    const { listRequests } = answerListPage({ list: () => noRepositories });
    renderRoute("/repositories?nameContains=kernel&isPublic=false");

    expect(
      await screen.findByRole(
        "heading",
        {
          level: 2,
          name: "No repositories match",
        },
        { timeout: firstRenderTimeout },
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Nothing matches the current filters."),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Clear filters" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Create a repository" }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("kernel");
    expect(
      screen.getByRole("combobox", { name: "Visibility" }),
    ).toHaveTextContent("Private");
    expect(Object.fromEntries(listRequests[0]!)).toEqual({
      nameContains: "kernel",
      isPublic: "false",
      pageSize: "50",
    });
  });

  it("tells an anonymous visitor there are no public repositories, with no action (list-empty-anonymous)", async () => {
    answerListPage({ list: () => noRepositories });
    renderRoute("/repositories");

    const heading = await screen.findByRole(
      "heading",
      { level: 2, name: "No public repositories" },
      { timeout: firstRenderTimeout },
    );
    expect(
      screen.getByText("There are no public repositories to show."),
    ).toBeInTheDocument();
    const emptyState = heading.parentElement!;
    expect(within(emptyState).queryByRole("button")).not.toBeInTheDocument();
    expect(within(emptyState).queryByRole("link")).not.toBeInTheDocument();
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

  it("reports the name search to the URL once typing stops, starting again at the first page", async () => {
    signInAs();
    const { listRequests } = answerListPage({
      list: pageOf(numberedRepositories(60)),
    });
    const { router } = renderRoute("/repositories?offset=50&sortBy=Created");
    await findTable();

    await userEvent.type(screen.getByRole("textbox", { name: "Name" }), "repo");

    await waitFor(() =>
      expect(router.state.location.search).toEqual({
        nameContains: "repo",
        sortBy: "Created",
      }),
    );
    await waitFor(() =>
      expect(listRequests.at(-1)?.get("nameContains")).toBe("repo"),
    );
    expect(
      listRequests.filter((query) => query.has("nameContains")),
    ).toHaveLength(1);
  });

  it("sets visibility in the URL and starts again at the first page", async () => {
    signInAs();
    answerListPage({ list: pageOf(numberedRepositories(60)) });
    const { router } = renderRoute("/repositories?offset=50");
    await findTable();

    await userEvent.click(screen.getByRole("combobox", { name: "Visibility" }));
    await userEvent.click(screen.getByRole("option", { name: "Private" }));

    await waitFor(() =>
      expect(router.state.location.search).toEqual({ isPublic: false }),
    );
  });

  it("sorts from a column header: another column without a direction, the active one reversed, and the first page", async () => {
    signInAs();
    const { listRequests } = answerListPage({
      list: pageOf(numberedRepositories(60)),
    });
    const { router } = renderRoute(
      "/repositories?offset=50&direction=Descending",
    );
    const table = await findTable();
    await waitFor(() => expect(rowNames(table)).toHaveLength(10));

    await userEvent.click(
      within(table).getByRole("button", { name: "Created" }),
    );
    await waitFor(() =>
      expect(router.state.location.search).toEqual({ sortBy: "Created" }),
    );

    // The answer still reports Name ascending as applied, so Name is the active header.
    await userEvent.click(within(table).getByRole("button", { name: "Name" }));
    await waitFor(() =>
      expect(router.state.location.search).toEqual({
        sortBy: "Name",
        direction: "Descending",
      }),
    );
    expect(Object.fromEntries(listRequests.at(-1)!)).toEqual({
      sortBy: "Name",
      direction: "Descending",
      pageSize: "50",
    });
  });

  it("moves to another page and page size through the URL", async () => {
    signInAs();
    answerListPage({ list: pageOf(numberedRepositories(60)) });
    const { router } = renderRoute("/repositories?nameContains=repo");
    const table = await findTable();
    await waitFor(() => expect(rowNames(table)).toHaveLength(50));

    await userEvent.click(
      screen.getByRole("button", { name: "Go to next page" }),
    );
    await waitFor(() => expect(rowNames(table)).toHaveLength(10));
    expect(router.state.location.search).toEqual({
      nameContains: "repo",
      offset: 50,
      pageSize: 50,
    });
    expect(screen.getByText("51–60 of 60")).toBeInTheDocument();

    await userEvent.click(
      screen.getByRole("combobox", { name: /Rows per page/ }),
    );
    await userEvent.click(screen.getByRole("option", { name: "25" }));
    await waitFor(() =>
      expect(router.state.location.search).toEqual({
        nameContains: "repo",
        pageSize: 25,
      }),
    );
  });

  it("removes every filter param, and keeps the sort, with Clear filters (list-empty-filtered)", async () => {
    signInAs();
    answerListPage({
      architectures: [anArchitecture(), anArchitecture({ value: "aarch64" })],
      list: (query) =>
        query.has("nameContains") ? noRepositories : aPage(repositories),
    });
    const { router } = renderRoute(
      `/repositories?nameContains=kernel&isPublic=false&ownerId=${me.id}&architecture=aarch64&sortBy=Updated`,
    );

    await userEvent.click(
      await screen.findByRole(
        "button",
        { name: "Clear filters" },
        { timeout: firstRenderTimeout },
      ),
    );

    await waitFor(() =>
      expect(router.state.location.search).toEqual({ sortBy: "Updated" }),
    );
    const table = await findTable();
    await waitFor(() => expect(rowNames(table)).toHaveLength(3));
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("");
  });

  it("offers the signed-in user first as (you), and filters by them", async () => {
    signInAs();
    const { userSearches } = answerListPage({ users: [sam, me] });
    const { router } = renderRoute("/repositories");
    await findTable();

    const owner = screen.getByRole("combobox", { name: "Owner" });
    await userEvent.click(owner);
    const options = await screen.findAllByRole("option");
    expect(options.map((option) => option.textContent)).toEqual([
      "Paul Trampert (you)",
      "Sam Lee",
    ]);
    expect(Object.fromEntries(userSearches[0]!)).toEqual({ pageSize: "20" });

    await userEvent.click(options[0]!);
    await waitFor(() =>
      expect(router.state.location.search).toEqual({ ownerId: me.id }),
    );
    expect(owner).toHaveValue("Paul Trampert (you)");
  });

  it("searches the owners as typed, and filters by another user", async () => {
    signInAs();
    const { userSearches } = answerListPage({
      users: [sam],
      knownUsers: [sam],
    });
    const { router } = renderRoute("/repositories");
    await findTable();

    await userEvent.type(
      screen.getByRole("combobox", { name: "Owner" }),
      "sam",
    );
    await waitFor(() =>
      expect(userSearches.at(-1)?.get("displayNameContains")).toBe("sam"),
    );
    expect(userSearches.at(-1)?.get("pageSize")).toBe("20");
    await userEvent.click(
      await screen.findByRole("option", { name: "Sam Lee" }),
    );

    await waitFor(() =>
      expect(router.state.location.search).toEqual({ ownerId: sam.id }),
    );
    await waitFor(() =>
      expect(screen.getByRole("combobox", { name: "Owner" })).toHaveValue(
        "Sam Lee",
      ),
    );
  });

  it("labels an ownerId from the URL with that user's name", async () => {
    signInAs();
    answerListPage({ knownUsers: [sam] });
    renderRoute(`/repositories?ownerId=${sam.id}`);
    await findTable();

    await waitFor(() =>
      expect(screen.getByRole("combobox", { name: "Owner" })).toHaveValue(
        "Sam Lee",
      ),
    );
  });

  it("removes an ownerId naming no user, in place of the history entry", async () => {
    signInAs();
    answerListPage();
    const { router } = renderRoute(
      `/repositories?ownerId=${sam.id}&nameContains=core`,
    );
    await findTable();

    await waitFor(() =>
      expect(router.state.location.search).toEqual({ nameContains: "core" }),
    );
    expect(router.history.length).toBe(1);
  });

  it("shows a spinner in the table's body until the first page arrives (list-loading)", async () => {
    signInAs();
    let release!: () => void;
    const released = new Promise<void>((resolve) => (release = resolve));
    answerListPage({
      list: async () => {
        await released;
        return aPage(repositories);
      },
    });
    renderRoute("/repositories");

    const table = await findTable();
    expect(table).toHaveAttribute("aria-busy", "true");
    expect(
      within(table).getByRole("progressbar", { name: "Loading" }),
    ).toBeInTheDocument();

    release();

    await waitFor(() =>
      expect(rowNames(table)).toEqual([
        "core-extras",
        "games",
        "my-aur-builds",
      ]),
    );
    expect(table).toHaveAttribute("aria-busy", "false");
  });

  it("keeps the filter bar and the previous rows, dimmed under a spinner, while a filter change loads (list-loading)", async () => {
    signInAs();
    let release!: () => void;
    const released = new Promise<void>((resolve) => (release = resolve));
    answerListPage({
      list: async (query) => {
        if (query.has("isPublic")) {
          await released;
          return aPage([myAurBuilds]);
        }
        return aPage(repositories);
      },
    });
    const { router } = renderRoute("/repositories");
    const table = await findTable();
    await waitFor(() => expect(rowNames(table)).toHaveLength(3));

    const name = screen.getByRole("textbox", { name: "Name" });
    await userEvent.type(name, "core");
    await act(() =>
      router.navigate({ to: "/repositories", search: { isPublic: false } }),
    );

    await waitFor(() => expect(table).toHaveAttribute("aria-busy", "true"));
    expect(screen.getByRole("table", { name: "Repositories" })).toBe(table);
    expect(screen.getByRole("textbox", { name: "Name" })).toBe(name);
    expect(name).toHaveValue("core");
    expect(rowNames(table)).toEqual(["core-extras", "games", "my-aur-builds"]);
    expect(
      screen.getByRole("progressbar", { name: "Loading" }),
    ).toBeInTheDocument();

    release();

    await waitFor(() => expect(rowNames(table)).toEqual(["my-aur-builds"]));
    expect(table).toHaveAttribute("aria-busy", "false");
    expect(name).toHaveValue("core");
  });

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

  it("shows a failed list as the error page in place of the table, and Try again fetches it again", async () => {
    signInAs();
    let failures = 0;
    answerListPage({
      list: () => {
        if (failures < 3) {
          failures++;
          return HttpResponse.json(
            { title: "The list failed.", status: 500 },
            { status: 500 },
          );
        }
        return aPage(repositories);
      },
    });
    renderRoute("/repositories");

    expect(
      await screen.findByRole(
        "button",
        { name: "Try again" },
        { timeout: 10_000 },
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("The list failed.");
    expect(screen.getByRole("textbox", { name: "Name" })).toBeInTheDocument();
    expect(
      screen.queryByRole("table", { name: "Repositories" }),
    ).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    const table = await findTable();
    await waitFor(() =>
      expect(rowNames(table)).toEqual([
        "core-extras",
        "games",
        "my-aur-builds",
      ]),
    );
  }, 15_000);
});
