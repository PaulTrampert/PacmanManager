import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from "@tanstack/react-router";
import { act, renderHook, waitFor } from "@testing-library/react";
import { createContext, createElement, use, type ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { useSessionControl } from "./useSessionControl";

const HookContent = createContext<ReactNode>(null);

/** The root route's component: whatever the wrapper hands it, which is the hook's host. */
function HookOutlet() {
  return use(HookContent);
}

/** Renders `useSessionControl` inside a router whose location is `path`. */
async function renderInRouter(
  path: string,
  onSignOut: () => void | Promise<void> = vi.fn(),
) {
  const router = createRouter({
    routeTree: createRootRoute({ component: HookOutlet }),
    history: createMemoryHistory({ initialEntries: [path] }),
  });
  const rendered = renderHook(() => useSessionControl(onSignOut), {
    wrapper: ({ children }) =>
      createElement(
        HookContent,
        { value: children },
        createElement(RouterProvider, { router }),
      ),
  });
  // The router resolves its first match asynchronously, so the hook renders once it has.
  await waitFor(() => expect(rendered.result.current).toBeDefined());
  return rendered;
}

describe("useSessionControl", () => {
  it("signs in returning to the current path and query", async () => {
    const { result } = await renderInRouter("/repositories?tab=packages");

    expect(result.current.signInHref).toBe(
      "/login?redirectUri=%2Frepositories%3Ftab%3Dpackages",
    );
  });

  it("starts with the menu closed", async () => {
    const { result } = await renderInRouter("/");

    expect(result.current.isOpen).toBe(false);
    expect(result.current.anchor).toBeNull();
  });

  it("opens the menu on the clicked element and closes it again", async () => {
    const { result } = await renderInRouter("/");
    const trigger = document.createElement("button");

    act(() => result.current.open({ currentTarget: trigger }));

    expect(result.current.isOpen).toBe(true);
    expect(result.current.anchor).toBe(trigger);

    act(() => result.current.close());

    expect(result.current.isOpen).toBe(false);
    expect(result.current.anchor).toBeNull();
  });

  it("closes the menu and calls onSignOut when signing out", async () => {
    const onSignOut = vi.fn();
    const { result } = await renderInRouter("/", onSignOut);
    act(() =>
      result.current.open({ currentTarget: document.createElement("button") }),
    );

    act(() => result.current.signOut());

    expect(result.current.isOpen).toBe(false);
    expect(onSignOut).toHaveBeenCalledOnce();
  });
});
