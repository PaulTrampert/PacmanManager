import Typography from "@mui/material/Typography";
import { queryOptions, useSuspenseQuery } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRoute,
  createRouter,
  RouterProvider,
} from "@tanstack/react-router";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { AppProviders } from "../AppProviders";
import { client, unwrap } from "../api";
import { apiHandler } from "../mocks/api";
import { server } from "../mocks/node";
import { notFoundOn404 } from "../notFoundOn404";
import { createQueryClient } from "../queryClient";
import { renderRoute } from "../testing/renderRoute";
import { Route as rootRoute } from "./__root";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

function repositoryQuery(id: string) {
  return queryOptions({
    queryKey: ["repositories", id],
    queryFn: () =>
      unwrap(client.GET("/v1/Repositories/{id}", { params: { path: { id } } })),
    // Retrying is the query client's business, and tested there; here, a failure is final.
    retry: false,
  });
}

// A page under the real root route whose loader fetches a repository, as a feature route's does.
const repositoryRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/repositories/$id",
  loader: ({ context, params }) =>
    context.queryClient.ensureQueryData(repositoryQuery(params.id)),
  onError: notFoundOn404,
  component: function RepositoryPage() {
    // The test's routes are not in the registered route tree, so the router's hooks cannot type
    // them; the page reads the query its loader ensured, as a feature page does.
    const { data: repository } = useSuspenseQuery(
      repositoryQuery(repositoryId),
    );
    return (
      <Typography variant="h4" component="h1">
        {repository.name}
      </Typography>
    );
  },
});

// A page that throws while rendering.
const brokenRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/broken",
  component: function BrokenPage(): never {
    throw new Error("The page broke.");
  },
});

function renderTestRoute(path: string) {
  const queryClient = createQueryClient();
  const router = createRouter({
    routeTree: rootRoute.addChildren([repositoryRoute, brokenRoute]),
    // The root route's beforeLoad replaces the session before any route reads it.
    context: { queryClient, session: { status: "anonymous" } },
    history: createMemoryHistory({ initialEntries: [path] }),
  });
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
}

/** Answers the repository request with each of `statuses` in turn, counting the requests. */
function answerRepositoryWith(...statuses: number[]) {
  const requests = { count: 0 };
  server.use(
    apiHandler("get", "/v1/Repositories/{id}", ({ params }) => {
      const status = statuses[Math.min(requests.count, statuses.length - 1)];
      requests.count++;
      return status === 200
        ? HttpResponse.json({ id: params.id, name: "core" })
        : HttpResponse.json(
            { title: `Failed with ${status}.`, status },
            { status },
          );
    }),
  );
  return requests;
}

describe("the root route", () => {
  it("shows a loader's 500 as the error page, and Try again fetches again", async () => {
    const requests = answerRepositoryWith(500, 200);
    renderTestRoute(`/repositories/${repositoryId}`);

    expect(
      await screen.findByRole("heading", {
        level: 1,
        name: "Something went wrong",
      }),
    ).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("Failed with 500.");
    expect(screen.getByRole("link", { name: "PacmanManager" })).toBeVisible();
    expect(requests.count).toBe(1);

    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    expect(
      await screen.findByRole("heading", { level: 1, name: "core" }),
    ).toBeInTheDocument();
    expect(requests.count).toBe(2);
  });

  it("shows a loader's 404 as the not-found page", async () => {
    const requests = answerRepositoryWith(404);
    renderTestRoute(`/repositories/${repositoryId}`);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Page not found" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "PacmanManager" })).toBeVisible();
    expect(requests.count).toBe(1);
  });

  it("shows a loader's 403 as the forbidden message", async () => {
    answerRepositoryWith(403);
    renderTestRoute(`/repositories/${repositoryId}`);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "You don't have permission to do that.",
    );
  });

  it("shows an exception thrown while rendering as the error page", async () => {
    renderTestRoute("/broken");

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "The page broke.",
    );
    expect(
      screen.getByRole("heading", { level: 1, name: "Something went wrong" }),
    ).toBeInTheDocument();
  });

  it("shows an unknown path as the not-found page, inside the app shell", async () => {
    renderRoute("/no/such/page");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Page not found" }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "PacmanManager" })).toHaveLength(
      1,
    );
  });
});
