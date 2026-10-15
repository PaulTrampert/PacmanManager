import type { Meta, StoryObj } from "@storybook/react-vite";
import { linkOptions } from "@tanstack/react-router";
import { delay, http, HttpResponse, type HttpHandler } from "msw";
import { useState } from "react";
import { expect, fn, mocked, screen, waitFor, within } from "storybook/test";
import type {
  Architecture,
  PublicUserInfo,
  Repository,
  RepositoryPaginatedResponse,
  RepositorySortField,
  SortDirection,
} from "../../api";
import { apiHandler, toMockPath } from "../../mocks/api";
import {
  anArchitecture,
  aPage,
  aRepository,
  aUser,
} from "../../mocks/fixtures";
import {
  anonymousUserinfoHandler,
  signedInHandlers,
} from "../../mocks/session";
import type { RepositoryListPageSearch } from "../../repositoryListSearch";
import {
  RepositoryListPage,
  type RepositoryListPageProps,
} from "./RepositoryListPage";

const me = aUser({
  id: "11111111-1111-4111-8111-111111111111",
  displayName: "Paul Trampert",
});
const sam = aUser({
  id: "33333333-3333-4333-8333-333333333333",
  displayName: "Sam Lee",
  email: "sam@example.com",
});
const everyone: PublicUserInfo[] = [me, sam];

function owner({ id, displayName }: PublicUserInfo) {
  return { id, displayName };
}

// Dates are at midday UTC, so each falls on the same day in any time zone the stories run in.
const coreExtras = aRepository({
  id: "a0000000-0000-4000-8000-000000000001",
  name: "core-extras",
  owner: owner(me),
  createdAt: "2026-03-02T12:00:00Z",
  updatedAt: "2026-10-10T12:00:00Z",
});
const games = aRepository({
  id: "a0000000-0000-4000-8000-000000000002",
  name: "games",
  owner: owner(sam),
  createdAt: "2026-06-18T12:00:00Z",
  updatedAt: "2026-09-30T12:00:00Z",
});
const myAurBuilds = aRepository({
  id: "a0000000-0000-4000-8000-000000000003",
  name: "my-aur-builds",
  owner: owner(me),
  isPublic: false,
  createdAt: "2026-04-11T12:00:00Z",
  updatedAt: "2026-10-12T12:00:00Z",
});
const repositories: Repository[] = [coreExtras, games, myAurBuilds];

/** `count` repositories owned by the signed-in user, named `repo-00` onwards. */
function numberedRepositories(count: number): Repository[] {
  return Array.from({ length: count }, (_, index) =>
    aRepository({
      id: `b0000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
      name: `repo-${String(index).padStart(2, "0")}`,
      owner: owner(me),
      createdAt: `2026-01-${String(index + 1).padStart(2, "0")}T12:00:00Z`,
      updatedAt: `2026-01-${String(index + 1).padStart(2, "0")}T12:00:00Z`,
    }),
  );
}

const defaultDirection: Record<RepositorySortField, SortDirection> = {
  Name: "Ascending",
  Created: "Descending",
  Updated: "Descending",
};

const sortKey: Record<RepositorySortField, (repository: Repository) => string> =
  {
    Name: (repository) => repository.name ?? "",
    Created: (repository) => repository.createdAt ?? "",
    Updated: (repository) => repository.updatedAt ?? "",
  };

/**
 * The answer `GET /v1/Repositories` gives over `all`: filtered, sorted with each field's default
 * direction, and paged, reporting the sort it applied.
 */
function listResponse(all: readonly Repository[], request: Request) {
  const query = new URL(request.url).searchParams;
  const sortBy = (query.get("sortBy") ?? "Name") as RepositorySortField;
  const direction = (query.get("direction") ??
    defaultDirection[sortBy]) as SortDirection;
  const nameContains = query.get("nameContains");
  const isPublic = query.get("isPublic");
  const ownerId = query.get("ownerId");
  const matching = all
    .filter(
      (repository) =>
        (nameContains === null ||
          (repository.name ?? "").includes(nameContains)) &&
        (isPublic === null || String(repository.isPublic) === isPublic) &&
        (ownerId === null || repository.owner?.id === ownerId),
    )
    .sort((a, b) => sortKey[sortBy](a).localeCompare(sortKey[sortBy](b)));
  if (direction === "Descending") {
    matching.reverse();
  }
  const offset = Number(query.get("offset") ?? 0);
  const pageSize = Number(query.get("pageSize"));
  return HttpResponse.json<RepositoryPaginatedResponse>(
    aPage<Repository>(matching.slice(offset, offset + pageSize), {
      sort: { sortBy, direction },
      offset,
      total: matching.length,
    }),
  );
}

/** Answers `GET /v1/Repositories` over `all`, as {@link listResponse} does. */
function listOf(all: readonly Repository[]): HttpHandler {
  return apiHandler("get", "/v1/Repositories", ({ request }) =>
    listResponse(all, request),
  );
}

/** Answers what decides the filters' state, and the owner search over `users`. */
function filterData({
  architectures = [anArchitecture()],
  users = everyone,
}: { architectures?: Architecture[]; users?: PublicUserInfo[] } = {}) {
  return [
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json(architectures),
    ),
    apiHandler("get", "/v1/Users", ({ request }) => {
      const text = new URL(request.url).searchParams.get("displayNameContains");
      const found = users.filter(
        (user) =>
          text === null ||
          user.displayName.toLowerCase().includes(text.toLowerCase()),
      );
      return HttpResponse.json(
        aPage<PublicUserInfo>(found, {
          sort: { sortBy: "DisplayName", direction: "Ascending" },
          total: users.length,
        }),
      );
    }),
    apiHandler("get", "/v1/Users/{userId}", ({ params }) => {
      if (params.userId === "me") {
        // The session's own record, which signedInHandlers answers.
        return undefined;
      }
      const user = users.find(({ id }) => id === params.userId);
      return user === undefined
        ? HttpResponse.json(
            { title: "Not Found", status: 404 },
            { status: 404 },
          )
        : HttpResponse.json(user);
    }),
  ];
}

/**
 * A {@link RepositoryListPage} that applies each search change it asks for, as the route does by
 * navigating. Each call of `onSearchChange` is recorded with an update that makes the search it
 * applied, so `play` can read it with {@link lastSearch}.
 */
function StatefulRepositoryListPage(props: RepositoryListPageProps) {
  const [search, setSearch] = useState(props.search);
  return (
    <RepositoryListPage
      {...props}
      search={search}
      onSearchChange={(update, options) => {
        const next = update(search);
        setSearch(next);
        props.onSearchChange(() => next, options);
      }}
    />
  );
}

/** The search params the page last asked for. */
function lastSearch(
  onSearchChange: RepositoryListPageProps["onSearchChange"],
): RepositoryListPageSearch | undefined {
  return mocked(onSearchChange).mock.lastCall?.[0]({});
}

/** The names in the table's rows, in order. */
function rowNames(table: HTMLElement): string[] {
  return within(table)
    .getAllByRole("row")
    .slice(1)
    .map((row) => within(row).getAllByRole("cell")[0]!.textContent);
}

const meta = {
  component: RepositoryListPage,
  args: {
    search: {},
    repositoryLink: (repository) =>
      linkOptions({ to: "/", hash: `repository-${repository.id}` }),
    newRepositoryLink: linkOptions({ to: "/", hash: "new-repository" }),
    onSearchChange: fn(),
  },
  render: (args) => <StatefulRepositoryListPage {...args} />,
  beforeEach: ({ msw }) => {
    msw.use(...signedInHandlers(me), ...filterData(), listOf(repositories));
  },
} satisfies Meta<typeof RepositoryListPage>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Wireframe: list-signed-in. Every filter, New repository, and the repositories by name. */
export const SignedIn: Story = {
  play: async ({ canvas }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() =>
      expect(rowNames(table)).toEqual([
        "core-extras",
        "games",
        "my-aur-builds",
      ]),
    );
    await expect(
      canvas.getByRole("heading", { level: 1, name: "Repositories" }),
    ).toBeInTheDocument();
    await expect(
      canvas.getByRole("link", { name: "New repository" }),
    ).toHaveAttribute("href", "/#new-repository");
    await expect(
      within(table).getByRole("link", { name: "games" }),
    ).toHaveAttribute("href", `/#repository-${games.id}`);
    await expect(
      canvas.getByRole("combobox", { name: "Visibility" }),
    ).toHaveTextContent("All");
    await expect(canvas.getByRole("combobox", { name: "Owner" })).toBeEnabled();
    await expect(
      canvas.getByRole("combobox", { name: "Architecture" }),
    ).toHaveAttribute("aria-disabled", "true");
  },
};

/** Wireframe: list-anonymous. No New repository, and visibility fixed on Public. */
export const Anonymous: Story = {
  beforeEach: ({ msw }) => {
    msw.use(anonymousUserinfoHandler(), listOf([coreExtras, games]));
  },
  play: async ({ canvas }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() =>
      expect(rowNames(table)).toEqual(["core-extras", "games"]),
    );
    await expect(
      canvas.queryByRole("link", { name: "New repository" }),
    ).not.toBeInTheDocument();
    const visibility = canvas.getByRole("combobox", { name: "Visibility" });
    await expect(visibility).toHaveAttribute("aria-disabled", "true");
    await expect(visibility).toHaveTextContent("Public");
  },
};

/** Wireframe: list-single-user. The owner filter is disabled on All. */
export const SingleUser: Story = {
  beforeEach: ({ msw }) => {
    msw.use(...filterData({ users: [me] }), listOf([coreExtras]));
  },
  play: async ({ canvas }) => {
    await canvas.findByRole("table", { name: "Repositories" });
    const owner = canvas.getByRole("combobox", { name: "Owner" });
    await expect(owner).toBeDisabled();
    await expect(owner).toHaveAttribute("placeholder", "All");
  },
};

/** Wireframe: list-own-repositories. The owner filter on the signed-in user, as "(you)". */
export const OwnRepositories: Story = {
  args: { search: { ownerId: me.id } },
  play: async ({ canvas }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() =>
      expect(rowNames(table)).toEqual(["core-extras", "my-aur-builds"]),
    );
    await expect(canvas.getByRole("combobox", { name: "Owner" })).toHaveValue(
      "Paul Trampert (you)",
    );
  },
};

/** An `ownerId` naming another user, labelled with their name. */
export const AnotherOwner: Story = {
  args: { search: { ownerId: sam.id } },
  play: async ({ canvas }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() => expect(rowNames(table)).toEqual(["games"]));
    await waitFor(() =>
      expect(canvas.getByRole("combobox", { name: "Owner" })).toHaveValue(
        "Sam Lee",
      ),
    );
  },
};

/** An `ownerId` naming no user is removed, in place of the history entry. */
export const UnknownOwner: Story = {
  args: { search: { ownerId: "99999999-9999-4999-8999-999999999999" } },
  play: async ({ args, canvas }) => {
    await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() => expect(args.onSearchChange).toHaveBeenCalledOnce());
    await expect(lastSearch(args.onSearchChange)).toEqual({});
    await expect(mocked(args.onSearchChange).mock.lastCall?.[1]).toEqual({
      replace: true,
    });
  },
};

/** Wireframe: list-owner-dropdown-open. "(you)" first, then the other users; each filters. */
export const OwnerDropdownOpen: Story = {
  play: async ({ args, canvas, userEvent }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() => expect(rowNames(table)).toHaveLength(3));

    const owner = canvas.getByRole("combobox", { name: "Owner" });
    await userEvent.click(owner);
    await waitFor(() =>
      expect(
        screen.getAllByRole("option").map((option) => option.textContent),
      ).toEqual(["Paul Trampert (you)", "Sam Lee"]),
    );

    await userEvent.click(screen.getByRole("option", { name: "Sam Lee" }));
    await waitFor(() =>
      expect(lastSearch(args.onSearchChange)).toEqual({ ownerId: sam.id }),
    );
    await waitFor(() => expect(rowNames(table)).toEqual(["games"]));
    await waitFor(() => expect(owner).toHaveValue("Sam Lee"));
  },
};

/** Filtering by name and visibility asks for each filter and starts again at the first page. */
export const Filtering: Story = {
  args: { search: { offset: 50, sortBy: "Created" } },
  beforeEach: ({ msw }) => {
    msw.use(listOf([...repositories, ...numberedRepositories(60)]));
  },
  play: async ({ args, canvas, userEvent }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() => expect(rowNames(table)).toHaveLength(13));

    await userEvent.type(canvas.getByRole("textbox", { name: "Name" }), "s");
    await waitFor(() =>
      expect(lastSearch(args.onSearchChange)).toEqual({
        nameContains: "s",
        sortBy: "Created",
      }),
    );
    await expect(args.onSearchChange).toHaveBeenCalledOnce();

    await userEvent.click(canvas.getByRole("combobox", { name: "Visibility" }));
    await userEvent.click(screen.getByRole("option", { name: "Private" }));
    await expect(lastSearch(args.onSearchChange)).toEqual({
      nameContains: "s",
      isPublic: false,
      sortBy: "Created",
    });
    await waitFor(() => expect(rowNames(table)).toEqual(["my-aur-builds"]));
  },
};

/**
 * Sorting from the column headers: another column without a direction, the active one reversed,
 * each from the first page.
 */
export const Sorting: Story = {
  args: { search: { offset: 0 } },
  play: async ({ args, canvas, userEvent }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() =>
      expect(rowNames(table)).toEqual([
        "core-extras",
        "games",
        "my-aur-builds",
      ]),
    );

    await userEvent.click(
      within(table).getByRole("button", { name: "Created" }),
    );
    await expect(lastSearch(args.onSearchChange)).toEqual({
      sortBy: "Created",
    });
    await waitFor(() =>
      expect(rowNames(table)).toEqual([
        "games",
        "my-aur-builds",
        "core-extras",
      ]),
    );

    await userEvent.click(
      within(table).getByRole("button", { name: "Created" }),
    );
    await expect(lastSearch(args.onSearchChange)).toEqual({
      sortBy: "Created",
      direction: "Ascending",
    });
    await waitFor(() =>
      expect(rowNames(table)).toEqual([
        "core-extras",
        "my-aur-builds",
        "games",
      ]),
    );
  },
};

/** Paging and the page size, through the search params. */
export const Paging: Story = {
  args: { search: { pageSize: 25 } },
  beforeEach: ({ msw }) => {
    msw.use(listOf(numberedRepositories(30)));
  },
  play: async ({ args, canvas, userEvent }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() => expect(rowNames(table)).toHaveLength(25));
    await expect(canvas.getByText("1–25 of 30")).toBeInTheDocument();

    await userEvent.click(
      canvas.getByRole("button", { name: "Go to next page" }),
    );
    await expect(lastSearch(args.onSearchChange)).toEqual({
      offset: 25,
      pageSize: 25,
    });
    await waitFor(() => expect(rowNames(table)).toHaveLength(5));
    await expect(canvas.getByText("26–30 of 30")).toBeInTheDocument();

    await userEvent.click(
      canvas.getByRole("combobox", { name: /Rows per page/ }),
    );
    await userEvent.click(screen.getByRole("option", { name: "50" }));
    await expect(lastSearch(args.onSearchChange)).toEqual({ pageSize: 50 });
    await waitFor(() => expect(rowNames(table)).toHaveLength(30));
  },
};

/** Wireframe: list-empty-signed-in. A signed-in user with no repositories is offered to create one. */
export const EmptySignedIn: Story = {
  beforeEach: ({ msw }) => {
    msw.use(listOf([]));
  },
  play: async ({ canvas }) => {
    await expect(
      await canvas.findByRole("heading", {
        level: 2,
        name: "No repositories yet",
      }),
    ).toBeInTheDocument();
    await expect(
      canvas.getByRole("link", { name: "Create a repository" }),
    ).toHaveAttribute("href", "/#new-repository");
    await expect(
      canvas.queryByRole("table", { name: "Repositories" }),
    ).not.toBeInTheDocument();
  },
};

/** Wireframe: list-empty-filtered. Clear filters removes every filter, keeping the sort. */
export const EmptyFiltered: Story = {
  args: {
    search: { nameContains: "kernel", isPublic: false, sortBy: "Updated" },
  },
  play: async ({ args, canvas, userEvent }) => {
    await expect(
      await canvas.findByRole("heading", {
        level: 2,
        name: "No repositories match",
      }),
    ).toBeInTheDocument();
    await expect(canvas.getByRole("textbox", { name: "Name" })).toHaveValue(
      "kernel",
    );

    await userEvent.click(
      canvas.getByRole("button", { name: "Clear filters" }),
    );

    await expect(lastSearch(args.onSearchChange)).toEqual({
      sortBy: "Updated",
    });
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() => expect(rowNames(table)).toHaveLength(3));
    await expect(canvas.getByRole("textbox", { name: "Name" })).toHaveValue("");
  },
};

/** Wireframe: list-empty-anonymous. No public repositories, and no action. */
export const EmptyAnonymous: Story = {
  beforeEach: ({ msw }) => {
    msw.use(anonymousUserinfoHandler(), listOf([]));
  },
  play: async ({ canvas }) => {
    const heading = await canvas.findByRole("heading", {
      level: 2,
      name: "No public repositories",
    });
    const emptyState = heading.parentElement!;
    await expect(
      within(emptyState).queryByRole("button"),
    ).not.toBeInTheDocument();
    await expect(
      within(emptyState).queryByRole("link"),
    ).not.toBeInTheDocument();
  },
};

/** Wireframe: list-loading. A spinner in the table's body until the first page arrives. */
export const FirstLoad: Story = {
  beforeEach: ({ msw }) => {
    msw.use(
      apiHandler("get", "/v1/Repositories", async ({ request }) => {
        await delay("infinite");
        return listResponse(repositories, request);
      }),
    );
  },
  play: async ({ canvas }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await expect(table).toHaveAttribute("aria-busy", "true");
    await expect(
      within(table).getByRole("progressbar", { name: "Loading" }),
    ).toBeInTheDocument();
  },
};

/**
 * Wireframe: list-loading. While a filter change loads, the filter bar stays with its typed text and
 * the previous rows stay, dimmed under a spinner.
 */
export const LoadingAfterFilterChange: Story = {
  beforeEach: ({ msw }) => {
    msw.use(
      apiHandler("get", "/v1/Repositories", async ({ request }) => {
        if (new URL(request.url).searchParams.has("isPublic")) {
          await delay("infinite");
        }
        return listResponse(repositories, request);
      }),
    );
  },
  play: async ({ canvas, userEvent }) => {
    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() => expect(rowNames(table)).toHaveLength(3));

    await userEvent.click(canvas.getByRole("combobox", { name: "Visibility" }));
    await userEvent.click(screen.getByRole("option", { name: "Private" }));

    await waitFor(() => expect(table).toHaveAttribute("aria-busy", "true"));
    await expect(rowNames(table)).toHaveLength(3);
    await expect(
      canvas.getByRole("progressbar", { name: "Loading" }),
    ).toBeInTheDocument();
  },
};

/** A failed list is shown as the error page in place of the table; Try again fetches it again. */
export const Failed: Story = {
  beforeEach: ({ msw }) => {
    let failures = 0;
    msw.use(
      // Untyped, so that it can answer with a 500: the document declares none but 200.
      http.get(toMockPath("/v1/Repositories"), ({ request }) => {
        if (failures < 3) {
          failures++;
          return HttpResponse.json(
            { title: "The list failed.", status: 500 },
            { status: 500 },
          );
        }
        return listResponse(repositories, request);
      }),
    );
  },
  play: async ({ canvas, userEvent }) => {
    const retry = await canvas.findByRole(
      "button",
      { name: "Try again" },
      { timeout: 10_000 },
    );
    await expect(canvas.getByRole("alert")).toHaveTextContent(
      "The list failed.",
    );
    await expect(
      canvas.getByRole("textbox", { name: "Name" }),
    ).toBeInTheDocument();

    await userEvent.click(retry);

    const table = await canvas.findByRole("table", { name: "Repositories" });
    await waitFor(() => expect(rowNames(table)).toHaveLength(3));
  },
};
