import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useDialog } from "./useDialog";

describe("useDialog", () => {
  it("cancels when dismissed with nothing in flight", () => {
    const onCancel = vi.fn();
    const { result } = renderHook(() =>
      useDialog({ confirmDisabled: false, onCancel, pending: false }),
    );

    result.current.onClose();

    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("ignores dismissal while a request is in flight", () => {
    const onCancel = vi.fn();
    const { result } = renderHook(() =>
      useDialog({ confirmDisabled: false, onCancel, pending: true }),
    );

    result.current.onClose();

    expect(onCancel).not.toHaveBeenCalled();
  });

  it("enables both actions when idle", () => {
    const { result } = renderHook(() =>
      useDialog({ confirmDisabled: false, onCancel: vi.fn(), pending: false }),
    );

    expect(result.current.cancelDisabled).toBe(false);
    expect(result.current.confirmDisabled).toBe(false);
  });

  it("disables only the confirming action when asked to", () => {
    const { result } = renderHook(() =>
      useDialog({ confirmDisabled: true, onCancel: vi.fn(), pending: false }),
    );

    expect(result.current.cancelDisabled).toBe(false);
    expect(result.current.confirmDisabled).toBe(true);
  });

  it("disables both actions while a request is in flight", () => {
    const { result } = renderHook(() =>
      useDialog({ confirmDisabled: false, onCancel: vi.fn(), pending: true }),
    );

    expect(result.current.cancelDisabled).toBe(true);
    expect(result.current.confirmDisabled).toBe(true);
  });

  it("follows pending as it changes", () => {
    const onCancel = vi.fn();
    const { result, rerender } = renderHook(
      ({ pending }) => useDialog({ confirmDisabled: false, onCancel, pending }),
      { initialProps: { pending: true } },
    );

    result.current.onClose();
    rerender({ pending: false });
    result.current.onClose();

    expect(onCancel).toHaveBeenCalledOnce();
    expect(result.current.cancelDisabled).toBe(false);
  });
});
