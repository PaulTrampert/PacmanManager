import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useCopyableCodeBlock } from "./useCopyableCodeBlock";

const text = "[repo]\nServer = https://example.test/repo\n";

function setClipboard(clipboard: Partial<Clipboard> | undefined) {
  Object.defineProperty(navigator, "clipboard", {
    value: clipboard,
    configurable: true,
  });
}

describe("useCopyableCodeBlock", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    // Remove the shadowing own property so the prototype's clipboard shows again.
    Reflect.deleteProperty(navigator, "clipboard");
  });

  it("starts idle and not blocked", () => {
    const { result } = renderHook(() => useCopyableCodeBlock(text, 1000));

    expect(result.current.outcome).toBe("idle");
    expect(result.current.blocked).toBe(false);
  });

  it("writes exactly the text and reports copied until the duration is up", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    setClipboard({ writeText });
    const { result } = renderHook(() => useCopyableCodeBlock(text, 1000));

    await act(() => result.current.copy());

    expect(writeText).toHaveBeenCalledExactlyOnceWith(text);
    expect(result.current.outcome).toBe("copied");
    expect(result.current.blocked).toBe(false);

    act(() => vi.advanceTimersByTime(1000));
    expect(result.current.outcome).toBe("idle");
  });

  it.each([
    ["is missing", undefined],
    ["rejects", { writeText: vi.fn().mockRejectedValue(new Error("denied")) }],
  ])(
    "selects the block and reports failed when the clipboard %s",
    async (_, clipboard) => {
      setClipboard(clipboard);
      const pre = document.createElement("pre");
      pre.textContent = text;
      document.body.append(pre);
      const { result } = renderHook(() => useCopyableCodeBlock(text, 1000));
      result.current.block.current = pre;

      await act(() => result.current.copy());

      expect(result.current.outcome).toBe("failed");
      expect(result.current.blocked).toBe(true);
      expect(window.getSelection()?.toString()).toBe(text);

      // The label reverts on the timer; the message stays until the next click.
      act(() => vi.advanceTimersByTime(1000));
      expect(result.current.outcome).toBe("idle");
      expect(result.current.blocked).toBe(true);

      pre.remove();
    },
  );

  it("clears the message when clicked again", async () => {
    setClipboard(undefined);
    const { result } = renderHook(() => useCopyableCodeBlock(text, 1000));
    await act(() => result.current.copy());
    expect(result.current.blocked).toBe(true);

    setClipboard({ writeText: vi.fn().mockResolvedValue(undefined) });
    await act(() => result.current.copy());

    expect(result.current.blocked).toBe(false);
    expect(result.current.outcome).toBe("copied");
  });
});
