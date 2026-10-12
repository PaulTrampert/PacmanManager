import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll, vi } from "vitest";
import { server } from "../mocks/node";

// jsdom does not implement scrolling, which the router's navigation calls.
vi.spyOn(window, "scrollTo").mockImplementation(() => {});

// jsdom leaves `Request` to Node's, which, unlike a browser's, rejects a relative URL such as the
// API client's. Resolve one against the page's address, as a browser does. This runs before any
// test module is imported, so the client picks the replacement up when it is created.
const NodeRequest = globalThis.Request;
globalThis.Request = class extends NodeRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    super(
      typeof input === "string" ? new URL(input, window.location.href) : input,
      init,
    );
  }
};

// A request no handler matches fails the test that made it.
beforeAll(() => server.listen({ onUnhandledRequest: "error" }));

afterEach(() => {
  cleanup();
  server.resetHandlers();
});

afterAll(() => server.close());
