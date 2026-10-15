import { linkOptions } from "@tanstack/react-router";
import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { afterEach, describe, expect, it } from "vitest";
import type { PublicUserInfo, Repository } from "../api";
import { apiHandler } from "../mocks/api";
import { anArchitecture, aPage, aRepository, aUser } from "../mocks/fixtures";
import { server } from "../mocks/node";
import { signedInHandlers } from "../mocks/session";
import { renderRoute } from "../testing/renderRoute";

const repositoryId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

/** Answers the page's requests: `repositoryId` is `core-extras`, and any other id is a `404`. */
function answerRequests() {
  server.use(
    apiHandler("get", "/v1/Repositories/{id}", ({ params }) =>
      params.id === repositoryId
        ? HttpResponse.json(
            aRepository({ id: repositoryId, name: "core-extras" }),
          )
        : HttpResponse.json(
            { title: "Not Found", status: 404 },
            { status: 404 },
          ),
    ),
    apiHandler("get", "/v1/Architectures", () =>
      HttpResponse.json([anArchitecture()]),
    ),
  );
}

describe("the repository route", () => {
  it("is the not-found page when the API answers 404", async () => {
    answerRequests();
    renderRoute("/repositories/00000000-0000-4000-8000-000000000000");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Page not found" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { level: 1, name: "core-extras" }),
    ).not.toBeInTheDocument();
  });

  it("is reached by a typed link, and gives the page its links", async () => {
    answerRequests();
    // Starts on a page that fetches nothing, as `/` no longer is: it redirects to the list.
    const { router } = renderRoute("/no-such-page");
    const link = linkOptions({
      to: "/repositories/$repositoryId",
      params: { repositoryId },
    });

    await act(() => router.navigate(link));

    expect(
      await screen.findByRole("heading", { level: 1, name: "core-extras" }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(
      `/repositories/${repositoryId}`,
    );
    const match = router.state.matches.at(-1);
    expect(match?.routeId).toBe("/repositories/$repositoryId/");
    expect(match?.params).toEqual({ repositoryId });
    const trail = screen.getByRole("navigation", { name: "breadcrumb" });
    expect(
      within(trail).getByRole("link", { name: "Repositories" }),
    ).toHaveAttribute("href", "/repositories");
    expect(screen.getByRole("link", { name: "Edit" })).toHaveAttribute(
      "href",
      `/repositories/${repositoryId}/edit`,
    );
  });
});

describe("deleting from the repository route", () => {
  let observer: MutationObserver | undefined;

  afterEach(() => {
    observer?.disconnect();
  });

  /** Records whether the root error component's heading is ever added to the document. */
  function watchForRootError(): { rendered: boolean } {
    const seen = { rendered: false };
    const check = () => {
      if (
        Array.from(document.querySelectorAll("h1")).some(
          (heading) => heading.textContent === "Something went wrong",
        )
      ) {
        seen.rendered = true;
      }
    };
    observer = new MutationObserver(check);
    observer.observe(document.body, { childList: true, subtree: true });
    return seen;
  }

  it("lands on the list, never rendering the root error component or fetching the deleted id again", async () => {
    let deleted = false;
    let detailRequestsAfterDelete = 0;
    server.use(
      ...signedInHandlers(aUser()),
      apiHandler("get", "/v1/Repositories/{id}", ({ params }) => {
        if (deleted) {
          detailRequestsAfterDelete += 1;
        }
        return params.id === repositoryId && !deleted
          ? HttpResponse.json(
              aRepository({ id: repositoryId, name: "core-extras" }),
            )
          : HttpResponse.json(
              { title: "Not Found", status: 404 },
              { status: 404 },
            );
      }),
      apiHandler("delete", "/v1/Repositories/{id}", () => {
        deleted = true;
        return new HttpResponse(null, { status: 204 });
      }),
      apiHandler("get", "/v1/Architectures", () =>
        HttpResponse.json([anArchitecture()]),
      ),
      apiHandler("get", "/v1/Repositories", () =>
        HttpResponse.json(
          aPage<Repository>([], {
            sort: { sortBy: "Name", direction: "Ascending" },
          }),
        ),
      ),
      apiHandler("get", "/v1/Users", () =>
        HttpResponse.json(
          aPage<PublicUserInfo>([], {
            sort: { sortBy: "DisplayName", direction: "Ascending" },
          }),
        ),
      ),
    );
    const rootError = watchForRootError();
    const user = userEvent.setup();
    const { router } = renderRoute(`/repositories/${repositoryId}`);

    await user.click(await screen.findByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog", {
      name: "Delete core-extras?",
    });
    await user.type(
      within(dialog).getByRole("textbox", {
        name: "Type core-extras to confirm",
      }),
      "core-extras",
    );
    await user.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(
      await screen.findByRole("heading", { level: 1, name: "Repositories" }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/repositories");
    await waitFor(() =>
      expect(router.state.matches.at(-1)?.routeId).toBe("/repositories/"),
    );
    expect(deleted).toBe(true);
    expect(detailRequestsAfterDelete).toBe(0);
    expect(rootError.rendered).toBe(false);
  });
});
