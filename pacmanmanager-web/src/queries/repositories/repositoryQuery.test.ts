import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { ApiError } from "../../api";
import { apiHandler } from "../../mocks/api";
import { aRepository } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { createQueryClient } from "../../queryClient";
import { repositoryQuery } from "./repositoryQuery";

const id = "4d3c2b1a-0f9e-4d8c-b7a6-5e4d3c2b1a0f";

describe("repositoryQuery", () => {
  it("is keyed under the repositories resource by id", () => {
    expect(repositoryQuery(id).queryKey).toEqual([
      "repositories",
      "detail",
      id,
    ]);
  });

  it("gets the repository by id", async () => {
    const repository = aRepository({ id, name: "core-extra" });
    const requested: URL[] = [];
    server.use(
      apiHandler("get", "/v1/Repositories/{id}", ({ request }) => {
        requested.push(new URL(request.url));
        return HttpResponse.json(repository);
      }),
    );

    const data = await createQueryClient().fetchQuery(repositoryQuery(id));

    expect(data).toEqual(repository);
    expect(requested).toHaveLength(1);
    expect(requested[0].pathname).toBe(
      `/api/pacmanmanager/v1/Repositories/${id}`,
    );
    expect(requested[0].search).toBe("");
  });

  it("fails with a 404 ApiError for a repository the caller cannot see", async () => {
    let requests = 0;
    server.use(
      apiHandler("get", "/v1/Repositories/{id}", () => {
        requests++;
        return HttpResponse.json(
          { title: "Not Found", status: 404 },
          { status: 404 },
        );
      }),
    );

    const error = await createQueryClient()
      .fetchQuery(repositoryQuery(id))
      .catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
    expect(requests).toBe(1);
  });
});
