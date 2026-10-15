import { act, renderHook } from "@testing-library/react";
import type { ChangeEvent } from "react";
import { describe, expect, it } from "vitest";
import { useDeleteRepositoryDialog } from "./useDeleteRepositoryDialog";

function change(value: string): ChangeEvent<HTMLInputElement> {
  return { target: { value } } as ChangeEvent<HTMLInputElement>;
}

describe("useDeleteRepositoryDialog", () => {
  it("starts empty and unconfirmed", () => {
    const { result } = renderHook(() =>
      useDeleteRepositoryDialog("core-extras", true),
    );

    expect(result.current.typed).toBe("");
    expect(result.current.confirmed).toBe(false);
  });

  it("confirms only the exact name", () => {
    const { result } = renderHook(() =>
      useDeleteRepositoryDialog("core-extras", true),
    );

    for (const value of [
      "core-extra",
      "Core-Extras",
      "core-extras ",
      " core-extras",
    ]) {
      act(() => result.current.onTypedChange(change(value)));
      expect(result.current.typed).toBe(value);
      expect(result.current.confirmed).toBe(false);
    }

    act(() => result.current.onTypedChange(change("core-extras")));
    expect(result.current.confirmed).toBe(true);
  });

  it.each([
    ["missing", undefined],
    ["empty", ""],
  ])("never confirms when the name is %s", (_, name) => {
    const { result } = renderHook(() => useDeleteRepositoryDialog(name, true));

    expect(result.current.confirmed).toBe(false);
    act(() => result.current.onTypedChange(change("core-extras")));
    expect(result.current.confirmed).toBe(false);
  });

  it("keeps what was typed while it stays open", () => {
    const { result, rerender } = renderHook(
      ({ open }) => useDeleteRepositoryDialog("core-extras", open),
      { initialProps: { open: true } },
    );

    act(() => result.current.onTypedChange(change("core-extras")));
    rerender({ open: true });

    expect(result.current.typed).toBe("core-extras");
    expect(result.current.confirmed).toBe(true);
  });

  it("clears what was typed when it opens again", () => {
    const { result, rerender } = renderHook(
      ({ open }) => useDeleteRepositoryDialog("core-extras", open),
      { initialProps: { open: true } },
    );

    act(() => result.current.onTypedChange(change("core-extras")));
    rerender({ open: false });
    rerender({ open: true });

    expect(result.current.typed).toBe("");
    expect(result.current.confirmed).toBe(false);
  });

  it("keeps its change handler while nothing it reads changes", () => {
    const { result, rerender } = renderHook(() =>
      useDeleteRepositoryDialog("core-extras", true),
    );
    const { onTypedChange } = result.current;

    rerender();

    expect(result.current.onTypedChange).toBe(onTypedChange);
  });
});
