import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useTemporaryFeedback } from "./useTemporaryFeedback";

describe("useTemporaryFeedback", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("starts at the resting value", () => {
    const { result } = renderHook(() => useTemporaryFeedback("idle", 1000));

    expect(result.current.value).toBe("idle");
  });

  it("shows a value until the duration is up, then reverts", () => {
    const { result } = renderHook(() => useTemporaryFeedback("idle", 1000));

    act(() => result.current.show("done"));
    expect(result.current.value).toBe("done");

    act(() => vi.advanceTimersByTime(999));
    expect(result.current.value).toBe("done");

    act(() => vi.advanceTimersByTime(1));
    expect(result.current.value).toBe("idle");
  });

  it("restarts the time when shown again", () => {
    const { result } = renderHook(() => useTemporaryFeedback("idle", 1000));

    act(() => result.current.show("one"));
    act(() => vi.advanceTimersByTime(800));
    act(() => result.current.show("two"));
    act(() => vi.advanceTimersByTime(800));
    expect(result.current.value).toBe("two");

    act(() => vi.advanceTimersByTime(200));
    expect(result.current.value).toBe("idle");
  });

  it("cancels the revert on unmount", () => {
    const { result, unmount } = renderHook(() =>
      useTemporaryFeedback("idle", 1000),
    );

    act(() => result.current.show("done"));
    unmount();

    expect(vi.getTimerCount()).toBe(0);
  });

  it("keeps show's identity across rerenders with new inputs", () => {
    const { result, rerender } = renderHook(
      ({ resting, duration }) => useTemporaryFeedback(resting, duration),
      { initialProps: { resting: { label: "idle" }, duration: 1000 } },
    );
    const { show } = result.current;

    rerender({ resting: { label: "idle" }, duration: 2000 });

    expect(result.current.show).toBe(show);
  });

  it("reverts to the resting value current when the revert fires", () => {
    const { result, rerender } = renderHook(
      ({ resting }) => useTemporaryFeedback(resting, 1000),
      { initialProps: { resting: "idle" } },
    );

    act(() => result.current.show("done"));
    rerender({ resting: "waiting" });
    act(() => vi.advanceTimersByTime(1000));

    expect(result.current.value).toBe("waiting");
  });

  it("uses the duration current when show is called", () => {
    const { result, rerender } = renderHook(
      ({ duration }) => useTemporaryFeedback("idle", duration),
      { initialProps: { duration: 1000 } },
    );

    rerender({ duration: 2000 });
    act(() => result.current.show("done"));
    act(() => vi.advanceTimersByTime(1999));
    expect(result.current.value).toBe("done");

    act(() => vi.advanceTimersByTime(1));
    expect(result.current.value).toBe("idle");
  });
});
