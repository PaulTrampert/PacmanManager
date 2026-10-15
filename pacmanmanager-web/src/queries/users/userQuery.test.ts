import { QueryClient } from "@tanstack/react-query";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { ApiError } from "../../api";
import { apiHandler } from "../../mocks/api";
import { aUser } from "../../mocks/fixtures";
import { server } from "../../mocks/node";
import { userQuery } from "./userQuery";

const id = "2b8e7c1a-3d4f-4e5a-9b6c-7d8e9f0a1b2c";

describe("userQuery", () => {
  it("asks for the user by id and answers with their record", async () => {
    const owner = aUser({ id, displayName: "Ada Lovelace" });
    const requested: string[] = [];
    server.use(
      apiHandler("get", "/v1/Users/{userId}", ({ params }) => {
        requested.push(params.userId);
        return HttpResponse.json(owner);
      }),
    );

    const user = await new QueryClient().fetchQuery(userQuery(id));

    expect(requested).toEqual([id]);
    expect(user).toEqual(owner);
  });

  it("keys the query by the users resource and the id", () => {
    expect(userQuery(id).queryKey).toEqual(["users", "detail", id]);
  });

  it("throws an ApiError when the user does not exist", async () => {
    server.use(
      apiHandler("get", "/v1/Users/{userId}", () =>
        HttpResponse.json({ title: "Not Found", status: 404 }, { status: 404 }),
      ),
    );

    const error = await new QueryClient()
      .fetchQuery(userQuery(id))
      .catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
  });
});
