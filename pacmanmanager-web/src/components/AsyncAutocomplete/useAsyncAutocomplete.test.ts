import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  searchDelayMs,
  useAsyncAutocomplete,
  type UseAsyncAutocompleteOptions,
} from "./useAsyncAutocomplete";

interface Person {
  id: string;
  name: string;
}

const me: Person = { id: "1", name: "Paul Trampert" };
const alex: Person = { id: "2", name: "Alex Morgan" };
const sam: Person = { id: "3", name: "Sam Lee" };

function setup(overrides: Partial<UseAsyncAutocompleteOptions<Person>> = {}) {
  const props: UseAsyncAutocompleteOptions<Person> = {
    value: undefined,
    onChange: vi.fn(),
    onSearch: vi.fn(),
    options: [me, alex, sam],
    pinnedOptions: [me],
    totalOptions: undefined,
    getOptionKey: (person) => person.id,
    getOptionSearchText: (person) => person.name,
    ...overrides,
  };
  const hook = renderHook(() => useAsyncAutocomplete(props));
  return { props, ...hook };
}

describe("useAsyncAutocomplete", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  describe("searching", () => {
    it("searches nothing while typing, and once for the whole text when typing stops", () => {
      const { props, result } = setup();

      act(() => result.current.onInputChange("a", "input"));
      act(() => vi.advanceTimersByTime(searchDelayMs - 1));
      act(() => result.current.onInputChange("al", "input"));
      act(() => vi.advanceTimersByTime(searchDelayMs - 1));

      expect(props.onSearch).not.toHaveBeenCalled();
      act(() => vi.advanceTimersByTime(1));
      expect(props.onSearch).toHaveBeenCalledExactlyOnceWith("al");
    });

    it("searches once per pause", () => {
      const { props, result } = setup();

      act(() => result.current.onInputChange("al", "input"));
      act(() => vi.advanceTimersByTime(searchDelayMs));
      act(() => result.current.onInputChange("alex", "input"));
      act(() => vi.advanceTimersByTime(searchDelayMs));

      expect(vi.mocked(props.onSearch).mock.calls).toEqual([["al"], ["alex"]]);
    });

    it("does not search again for the text last searched for", () => {
      const { props, result } = setup();

      act(() => result.current.onInputChange("al", "input"));
      act(() => vi.advanceTimersByTime(searchDelayMs));
      act(() => result.current.onInputChange("ale", "input"));
      act(() => result.current.onInputChange("al", "input"));
      act(() => vi.advanceTimersByTime(searchDelayMs));

      expect(props.onSearch).toHaveBeenCalledOnce();
    });

    it("does not search for empty text before anything was searched for", () => {
      const { props, result } = setup();

      act(() => result.current.onInputChange("", "input"));
      act(() => vi.advanceTimersByTime(searchDelayMs));

      expect(props.onSearch).not.toHaveBeenCalled();
    });
  });

  describe("resetting the search", () => {
    it.each(["selectOption", "reset", "clear", "blur"] as const)(
      "searches again with no text when the text changes by %s",
      (reason) => {
        const { props, result } = setup();
        act(() => result.current.onInputChange("al", "input"));
        act(() => vi.advanceTimersByTime(searchDelayMs));

        act(() => result.current.onInputChange("Alex Morgan", reason));
        act(() => vi.advanceTimersByTime(searchDelayMs));

        expect(props.onSearch).toHaveBeenLastCalledWith("");
        expect(props.onSearch).toHaveBeenCalledTimes(2);
      },
    );

    it("does not search when the text was not searched for yet", () => {
      const { props, result } = setup();

      act(() => result.current.onInputChange("al", "input"));
      act(() => result.current.onInputChange("Alex Morgan", "selectOption"));
      act(() => vi.advanceTimersByTime(searchDelayMs));

      expect(props.onSearch).not.toHaveBeenCalled();
    });

    it("lists every pinned option again, rather than matching the chosen label", () => {
      const { result } = setup({ options: [alex] });
      act(() => result.current.onInputChange("alex", "input"));
      expect(result.current.options).toEqual([alex]);

      act(() => result.current.onInputChange("Alex Morgan", "selectOption"));

      expect(result.current.options).toEqual([me, alex]);
    });
  });

  describe("options", () => {
    it("lists matching pinned options first, then the results less any pinned option", () => {
      const { result } = setup();

      expect(result.current.options).toEqual([me, alex, sam]);
      expect(result.current.hasDividerAfter(me)).toBe(true);
      expect(result.current.hasDividerAfter(alex)).toBe(false);
    });

    it("matches pinned options against the typed text, with no divider when none match", () => {
      const { result } = setup({ options: [alex] });

      act(() => result.current.onInputChange("al", "input"));

      expect(result.current.options).toEqual([alex]);
      expect(result.current.hasDividerAfter(alex)).toBe(false);
    });

    it("puts no divider after the pinned options when there are no results", () => {
      const { result } = setup({ options: [] });

      expect(result.current.options).toEqual([me]);
      expect(result.current.hasDividerAfter(me)).toBe(false);
    });

    it("compares options by key", () => {
      const { result } = setup();

      expect(result.current.isOptionEqualToValue(me, { ...me })).toBe(true);
      expect(result.current.isOptionEqualToValue(me, alex)).toBe(false);
    });
  });

  describe("value", () => {
    it("shows the chosen option", () => {
      expect(setup({ value: alex }).result.current.value).toBe(alex);
    });

    it("shows nothing for All", () => {
      expect(setup().result.current.value).toBeNull();
    });

    it("reports a choice, and clearing the field as undefined", () => {
      const { props, result } = setup();

      result.current.onChange(alex);
      result.current.onChange(null);

      expect(vi.mocked(props.onChange).mock.calls).toEqual([
        [alex],
        [undefined],
      ]);
    });
  });

  describe("disabled", () => {
    it.each([0, 1])(
      "is disabled on All, whatever the value, with %i options in all",
      (totalOptions) => {
        const { result } = setup({ totalOptions, value: alex });

        expect(result.current.disabled).toBe(true);
        expect(result.current.value).toBeNull();
      },
    );

    it("is enabled with more than one option in all", () => {
      const { result } = setup({ totalOptions: 2, value: alex });

      expect(result.current.disabled).toBe(false);
      expect(result.current.value).toBe(alex);
    });

    it("is enabled when the number of options is unknown", () => {
      expect(setup().result.current.disabled).toBe(false);
    });
  });
});
