import { screen } from "@testing-library/react";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { PublicUserInfo } from "../api";
import { apiHandler } from "../mocks/api";
import { anArchitecture, aPage, aRepository } from "../mocks/fixtures";
import { server } from "../mocks/node";
import { renderRoute } from "../testing/renderRoute";

describe("the index route", () => {
  it("redirects to the repository list, replacing its own history entry", async () => {
    server.use(
      apiHandler("get", "/v1/Architectures", () =>
        HttpResponse.json([anArchitecture()]),
      ),
      apiHandler("get", "/v1/Users", () =>
        HttpResponse.json(
          aPage<PublicUserInfo>([], {
            sort: { sortBy: "DisplayName", direction: "Ascending" },
            total: 2,
          }),
        ),
      ),
      apiHandler("get", "/v1/Repositories", () =>
        HttpResponse.json(aPage([aRepository({ name: "core" })])),
      ),
    );

    const { router } = renderRoute("/");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Repositories" }),
    ).toBeInTheDocument();
    expect(await screen.findByRole("link", { name: "core" })).toBeVisible();
    expect(router.state.location.pathname).toBe("/repositories");
    expect(router.history.length).toBe(1);
    expect(screen.getByRole("link", { name: "PacmanManager" })).toHaveAttribute(
      "href",
      "/",
    );
  });
});
