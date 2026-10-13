import { describe, expect, it } from "vitest";
import { visibleOptions } from "./visibleOptions";

interface Person {
  id: string;
  name: string;
}

const me: Person = { id: "1", name: "Paul Trampert" };
const alex: Person = { id: "2", name: "Alex Morgan" };
const sam: Person = { id: "3", name: "Sam Lee" };

function visible(pinned: Person[], results: Person[], text: string) {
  return visibleOptions(
    pinned,
    results,
    text,
    (person) => person.id,
    (person) => person.name,
  );
}

describe("visibleOptions", () => {
  it("shows every pinned option above the results when no text is typed", () => {
    expect(visible([me], [alex, sam], "")).toEqual({
      pinned: [me],
      results: [alex, sam],
    });
  });

  it("treats whitespace alone as no text", () => {
    expect(visible([me], [], "  ").pinned).toEqual([me]);
  });

  it("shows a pinned option whose text contains the typed text, ignoring case", () => {
    expect(visible([me], [], "TRAMP").pinned).toEqual([me]);
  });

  it("hides a pinned option whose text does not contain the typed text", () => {
    expect(visible([me], [alex], "alex")).toEqual({
      pinned: [],
      results: [alex],
    });
  });

  it("drops a search result that is a pinned option, so it appears once", () => {
    expect(visible([me], [alex, me, sam], "")).toEqual({
      pinned: [me],
      results: [alex, sam],
    });
  });

  it("drops a pinned search result even while the pinned option is hidden", () => {
    expect(visible([me], [me, alex], "alex").results).toEqual([alex]);
  });

  it("compares pinned options and results by key, not by identity", () => {
    expect(visible([me], [{ ...me }, sam], "").results).toEqual([sam]);
  });

  it("passes the results through when nothing is pinned", () => {
    expect(visible([], [alex, sam], "zzz")).toEqual({
      pinned: [],
      results: [alex, sam],
    });
  });
});
