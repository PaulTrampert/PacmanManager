import { renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useDebouncedCallback } from "./useDebouncedCallback";

describe("useDebouncedCallback", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("calls nothing until the delay has passed", () => {
    const callback = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(callback, 300));

    result.current("a");
    vi.advanceTimersByTime(299);

    expect(callback).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(callback).toHaveBeenCalledExactlyOnceWith("a");
  });

  it("calls once, with the latest arguments, for calls closer together than the delay", () => {
    const callback = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(callback, 300));

    result.current("a");
    vi.advanceTimersByTime(200);
    result.current("ab");
    vi.advanceTimersByTime(200);
    result.current("abc");
    vi.advanceTimersByTime(300);

    expect(callback).toHaveBeenCalledExactlyOnceWith("abc");
  });

  it("calls once per pause", () => {
    const callback = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(callback, 300));

    result.current("a");
    vi.advanceTimersByTime(300);
    result.current("b");
    vi.advanceTimersByTime(300);

    expect(callback.mock.calls).toEqual([["a"], ["b"]]);
  });

  it("calls the callback of the latest render", () => {
    const first = vi.fn();
    const second = vi.fn();
    const { result, rerender } = renderHook(
      ({ callback }) => useDebouncedCallback(callback, 300),
      { initialProps: { callback: first } },
    );

    result.current("a");
    rerender({ callback: second });
    vi.advanceTimersByTime(300);

    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledExactlyOnceWith("a");
  });

  it("keeps the same function across renders", () => {
    const { result, rerender } = renderHook(
      ({ callback }) => useDebouncedCallback(callback, 300),
      { initialProps: { callback: vi.fn() } },
    );
    const first = result.current;

    rerender({ callback: vi.fn() });

    expect(result.current).toBe(first);
  });

  it("calls nothing once unmounted", () => {
    const callback = vi.fn();
    const { result, unmount } = renderHook(() =>
      useDebouncedCallback(callback, 300),
    );

    result.current("a");
    unmount();
    vi.advanceTimersByTime(300);

    expect(callback).not.toHaveBeenCalled();
  });
});
