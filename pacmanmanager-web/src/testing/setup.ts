import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll, vi } from "vitest";
import { server } from "../mocks/node";

// jsdom does not implement scrolling, which the router's navigation calls.
vi.spyOn(window, "scrollTo").mockImplementation(() => {});

// A request no handler matches fails the test that made it.
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));

afterEach(() => {
  cleanup();
  server.resetHandlers();
});

afterAll(() => server.close());
