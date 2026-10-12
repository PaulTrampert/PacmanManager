import { describe, expect, it } from "vitest";
import { ApiError } from "./ApiError";
import { unwrap } from "./unwrap";

describe("unwrap", () => {
  it("returns the data of a successful response", async () => {
    const data = { id: "1" };

    await expect(
      unwrap(Promise.resolve({ data, response: new Response(null) })),
    ).resolves.toBe(data);
  });

  it("returns undefined for a successful response without a body", async () => {
    await expect(
      unwrap(
        Promise.resolve({ response: new Response(null, { status: 204 }) }),
      ),
    ).resolves.toBeUndefined();
  });

  it("throws an ApiError for an error response without a body", async () => {
    const pending = unwrap(
      Promise.resolve({ response: new Response(null, { status: 503 }) }),
    );

    await expect(pending).rejects.toBeInstanceOf(ApiError);
    await expect(pending).rejects.toMatchObject({ status: 503 });
  });

  it("passes on a network failure unchanged", async () => {
    const failure = new TypeError("Failed to fetch");

    await expect(unwrap(Promise.reject(failure))).rejects.toBe(failure);
  });
});
