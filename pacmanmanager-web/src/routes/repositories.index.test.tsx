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

function signInAs(user = me) {
  server.use(...signedInHandlers(user));
}

async function findTable() {
  return screen.findByRole("table", { name: "Repositories" });
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
      await screen.findByRole("heading", {
        level: 2,
        name: "No repositories yet",
      }),
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
      await screen.findByRole("heading", {
        level: 2,
        name: "No repositories match",
      }),
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

    const heading = await screen.findByRole("heading", {
      level: 2,
      name: "No public repositories",
    });
    expect(
      screen.getByText("There are no public repositories to show."),
    ).toBeInTheDocument();
    const emptyState = heading.parentElement!;
    expect(within(emptyState).queryByRole("button")).not.toBeInTheDocument();
    expect(within(emptyState).queryByRole("link")).not.toBeInTheDocument();
  });

  it("does not show an empty state for a page past the end", async () => {
    signInAs();
    answerListPage({
      list: () =>
        aPage<Repository>([], {
          sort: { sortBy: "Name", direction: "Ascending" },
          offset: 500,
          total: 3,
        }),
    });
    renderRoute("/repositories?offset=500");

    await findTable();
    expect(screen.queryByRole("heading", { level: 2 })).not.toBeInTheDocument();
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
