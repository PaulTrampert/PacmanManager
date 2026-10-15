import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { anArchitecture, aUser } from "../../mocks/fixtures";
import type { Session } from "../../session";
import {
  searchDelayMs,
  useRepositoryFilters,
  type UseRepositoryFiltersOptions,
} from "./useRepositoryFilters";

const me = aUser({
  id: "11111111-1111-1111-1111-111111111111",
  displayName: "Paul Trampert",
});
const alex = aUser({
  id: "22222222-2222-2222-2222-222222222222",
  displayName: "Alex Morgan",
});
const signedIn: Session = { status: "signedIn", user: me };
const anonymous: Session = { status: "anonymous" };

function setup(overrides: Partial<UseRepositoryFiltersOptions> = {}) {
  const props: UseRepositoryFiltersOptions = {
    filters: {},
    onChange: vi.fn(),
    session: signedIn,
    architectures: [anArchitecture()],
    owner: undefined,
    ...overrides,
  };
  const hook = renderHook(
    (current: UseRepositoryFiltersOptions) => useRepositoryFilters(current),
    { initialProps: props },
  );
  return { props, ...hook };
}

describe("useRepositoryFilters", () => {
  describe("name search", () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it("starts from the applied nameContains", () => {
      const { result } = setup({ filters: { nameContains: "core" } });
      expect(result.current.name.text).toBe("core");
    });

    it("shows typing at once and reports it once typing stops", () => {
      const { props, result } = setup();

      act(() => result.current.name.onChange("c"));
      act(() => vi.advanceTimersByTime(searchDelayMs - 1));
      act(() => result.current.name.onChange("co"));
      expect(result.current.name.text).toBe("co");
      act(() => vi.advanceTimersByTime(searchDelayMs - 1));
      expect(props.onChange).not.toHaveBeenCalled();

      act(() => vi.advanceTimersByTime(1));
      expect(props.onChange).toHaveBeenCalledExactlyOnceWith({
        nameContains: "co",
      });
    });

    it("reports an emptied field as undefined", () => {
      const { props, result } = setup({ filters: { nameContains: "core" } });

      act(() => result.current.name.onChange(""));
      act(() => vi.advanceTimersByTime(searchDelayMs));

      expect(props.onChange).toHaveBeenCalledExactlyOnceWith({
        nameContains: undefined,
      });
    });

    it("does not report text that comes back to the value last reported", () => {
      const { props, result } = setup();

      act(() => result.current.name.onChange("c"));
      act(() => result.current.name.onChange(""));
      act(() => vi.advanceTimersByTime(searchDelayMs));

      expect(props.onChange).not.toHaveBeenCalled();
    });

    it("keeps the typed text when the caller applies what it reported", () => {
      const { props, result, rerender } = setup();

      act(() => result.current.name.onChange("core"));
      act(() => vi.advanceTimersByTime(searchDelayMs));
      act(() => result.current.name.onChange("core-"));
      rerender({ ...props, filters: { nameContains: "core" } });

      expect(result.current.name.text).toBe("core-");
    });

    it("takes a nameContains the caller changes by other means", () => {
      const { props, result, rerender } = setup({
        filters: { nameContains: "core" },
      });

      rerender({ ...props, filters: {} });

      expect(result.current.name.text).toBe("");
    });
  });

  describe("visibility", () => {
    it("maps isPublic to the select's values and back", () => {
      const { props, result } = setup({ filters: { isPublic: false } });
      expect(result.current.visibility.value).toBe("private");
      expect(result.current.visibility.disabledValue).toBeUndefined();

      result.current.visibility.onChange("public");
      result.current.visibility.onChange(undefined);

      expect(vi.mocked(props.onChange).mock.calls).toEqual([
        [{ isPublic: true }],
        [{ isPublic: undefined }],
      ]);
    });

    it("is disabled on Public for an anonymous visitor", () => {
      const { result } = setup({ session: anonymous });
      expect(result.current.visibility.disabledValue).toBe("public");
    });
  });

  describe("owner", () => {
    it("pins the signed-in user, labelled as you", () => {
      const { result } = setup();

      expect(result.current.owner.pinned).toEqual([
        { id: me.id, displayName: me.displayName },
      ]);
      expect(result.current.owner.getOptionLabel(me)).toBe(
        "Paul Trampert (you)",
      );
      expect(result.current.owner.getOptionLabel(alex)).toBe("Alex Morgan");
    });

    it("pins nobody for an anonymous visitor", () => {
      const { result } = setup({ session: anonymous });
      expect(result.current.owner.pinned).toEqual([]);
      expect(result.current.owner.getOptionLabel(me)).toBe("Paul Trampert");
    });

    it("shows the signed-in user for their own id", () => {
      const { result } = setup({ filters: { ownerId: me.id } });
      expect(result.current.owner.value).toEqual({
        id: me.id,
        displayName: me.displayName,
      });
    });

    it("shows the given owner only when it is the one filtered by", () => {
      expect(
        setup({ filters: { ownerId: alex.id }, owner: alex }).result.current
          .owner.value,
      ).toBe(alex);
      expect(
        setup({ filters: { ownerId: alex.id } }).result.current.owner.value,
      ).toBeUndefined();
      expect(
        setup({ filters: { ownerId: "someone-else" }, owner: alex }).result
          .current.owner.value,
      ).toBeUndefined();
      expect(setup({ owner: alex }).result.current.owner.value).toBeUndefined();
    });

    it("reports the chosen user's id, and undefined once cleared", () => {
      const { props, result } = setup();

      result.current.owner.onChange(alex);
      result.current.owner.onChange(undefined);

      expect(vi.mocked(props.onChange).mock.calls).toEqual([
        [{ ownerId: alex.id }],
        [{ ownerId: undefined }],
      ]);
    });
  });

  describe("architecture", () => {
    it("lists the architectures by display value and reports the chosen one", () => {
      const aarch64 = anArchitecture({
        value: "aarch64",
        displayValue: "64-bit ARM (aarch64)",
      });
      const { props, result } = setup({
        architectures: [anArchitecture(), aarch64],
        filters: { architecture: "aarch64" },
      });

      expect(result.current.architecture.options).toEqual([
        { value: "x86_64", label: "64-bit Intel/AMD (x86_64)" },
        { value: "aarch64", label: "64-bit ARM (aarch64)" },
      ]);
      expect(result.current.architecture.value).toBe("aarch64");

      result.current.architecture.onChange(undefined);
      expect(props.onChange).toHaveBeenCalledExactlyOnceWith({
        architecture: undefined,
      });
    });
  });
});
