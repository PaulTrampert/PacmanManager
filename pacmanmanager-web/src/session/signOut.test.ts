import { afterEach, describe, expect, it, vi } from "vitest";
import { antiforgeryHandler } from "../mocks/api";
import { server } from "../mocks/node";
import { signOut } from "./signOut";

describe("signOut", () => {
  // jsdom does not implement submitting a form, which would leave the page in a browser.
  const submit = vi
    .spyOn(HTMLFormElement.prototype, "submit")
    .mockImplementation(() => {});

  afterEach(() => {
    submit.mockClear();
    document.body.replaceChildren();
  });

  it("posts a form to /logout carrying a fresh antiforgery token", async () => {
    server.use(antiforgeryHandler("fresh-token"));

    await signOut();

    expect(submit).toHaveBeenCalledOnce();
    const form = submit.mock.contexts[0] as HTMLFormElement;
    expect(form.method).toBe("post");
    expect(new URL(form.action).pathname).toBe("/logout");
    expect(Object.fromEntries(new FormData(form))).toEqual({
      antiforgeryToken: "fresh-token",
    });
  });
});
