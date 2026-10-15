import { describe, expect, it } from "vitest";
import { anArchitecture, aUser } from "./mocks/fixtures";
import {
  hasDroppedParams,
  hasRepositoryListFilters,
  repositoryListRequest,
  validateRepositoryListSearch,
  withoutDisabledFilters,
  type RepositoryListFilterState,
  type RepositoryListPageSearch,
} from "./repositoryListSearch";

const ownerId = "6f1c1f0e-8a52-4a3b-9a57-2c5d3e8f9b10";

describe("validateRepositoryListSearch", () => {
  it("keeps every param the API accepts", () => {
    const search = {
      nameContains: "core",
      isPublic: false,
      ownerId,
      architecture: "aarch64",
      sortBy: "Updated",
      direction: "Ascending",
      offset: 100,
      pageSize: 25,
    };

    expect(validateRepositoryListSearch(search)).toEqual(search);
  });

  it("is empty for no params", () => {
    expect(validateRepositoryListSearch({})).toEqual({});
  });

  it.each([25, 50, 100])("keeps a page size of %i", (pageSize) => {
    expect(validateRepositoryListSearch({ pageSize })).toEqual({ pageSize });
  });

  it.each([7, 0, 500, "50", 50.5, null])(
    "drops a page size of %j",
    (pageSize) => {
      expect(validateRepositoryListSearch({ pageSize })).toEqual({});
    },
  );

  it.each([-1, 2.5, "10", null])("drops an offset of %j", (offset) => {
    expect(validateRepositoryListSearch({ offset })).toEqual({});
  });

  it("drops a sort field or direction the API does not have", () => {
    expect(
      validateRepositoryListSearch({ sortBy: "Owner", direction: "Up" }),
    ).toEqual({});
  });

  it("drops a visibility that is not a boolean", () => {
    expect(validateRepositoryListSearch({ isPublic: "yes" })).toEqual({});
  });

  it("drops empty text", () => {
    expect(
      validateRepositoryListSearch({
        nameContains: "",
        ownerId: "",
        architecture: "",
      }),
    ).toEqual({});
  });

  it("keeps text the router parsed as a number as the text it was", () => {
    expect(validateRepositoryListSearch({ nameContains: 123 })).toEqual({
      nameContains: "123",
    });
  });

  it("drops params it does not know", () => {
    expect(validateRepositoryListSearch({ tab: "packages" })).toEqual({});
  });
});

describe("hasDroppedParams", () => {
  it("is false when validation kept every param", () => {
    const given = { nameContains: "core", pageSize: 25 };

    expect(hasDroppedParams(given, validateRepositoryListSearch(given))).toBe(
      false,
    );
  });

  it("is false when validation only turned a param into text", () => {
    const given = { nameContains: 123 };

    expect(hasDroppedParams(given, validateRepositoryListSearch(given))).toBe(
      false,
    );
  });

  it.each<Record<string, unknown>>([
    { pageSize: 7 },
    { isPublic: "yes", nameContains: "core" },
    { tab: "packages" },
  ])("is true when validation dropped a param of %j", (given) => {
    expect(hasDroppedParams(given, validateRepositoryListSearch(given))).toBe(
      true,
    );
  });
});

describe("withoutDisabledFilters", () => {
  const twoArchitectures = [
    anArchitecture(),
    anArchitecture({ value: "aarch64", displayValue: "64-bit ARM (aarch64)" }),
  ];
  const everythingEnabled: RepositoryListFilterState = {
    session: { status: "signedIn", user: aUser() },
    architectures: twoArchitectures,
    userCount: 2,
  };
  const filtered: RepositoryListPageSearch = {
    nameContains: "core",
    isPublic: false,
    ownerId,
    architecture: "x86_64",
    offset: 50,
  };

  it("returns the search itself when no disabled filter is present", () => {
    expect(withoutDisabledFilters(filtered, everythingEnabled)).toBe(filtered);
  });

  it("returns the search itself when a disabled filter is absent", () => {
    const search: RepositoryListPageSearch = { nameContains: "core" };

    expect(
      withoutDisabledFilters(search, {
        session: { status: "anonymous" },
        architectures: [anArchitecture()],
        userCount: 1,
      }),
    ).toBe(search);
  });

  it("removes architecture with fewer than two architectures", () => {
    expect(
      withoutDisabledFilters(filtered, {
        ...everythingEnabled,
        architectures: [anArchitecture()],
      }),
    ).toEqual({ ...filtered, architecture: undefined });
  });

  it("removes ownerId with fewer than two users", () => {
    expect(
      withoutDisabledFilters(filtered, { ...everythingEnabled, userCount: 1 }),
    ).toEqual({ ...filtered, ownerId: undefined });
  });

  it("removes isPublic for an anonymous visitor", () => {
    expect(
      withoutDisabledFilters(filtered, {
        ...everythingEnabled,
        session: { status: "anonymous" },
      }),
    ).toEqual({ ...filtered, isPublic: undefined });
  });

  it("removes every disabled filter's param at once, and leaves the search it was given alone", () => {
    const search = { ...filtered };

    const result = withoutDisabledFilters(search, {
      session: { status: "anonymous" },
      architectures: [],
      userCount: 0,
    });

    expect(Object.keys(result)).toEqual(["nameContains", "offset"]);
    expect(search).toEqual(filtered);
  });
});

describe("hasRepositoryListFilters", () => {
  it("is false without filter params, whatever the sort and paging", () => {
    expect(
      hasRepositoryListFilters({
        sortBy: "Created",
        direction: "Descending",
        offset: 50,
        pageSize: 25,
      }),
    ).toBe(false);
  });

  it.each<RepositoryListPageSearch>([
    { nameContains: "core" },
    { isPublic: false },
    { ownerId },
    { architecture: "x86_64" },
  ])("is true with %j", (search) => {
    expect(hasRepositoryListFilters(search)).toBe(true);
  });
});

describe("repositoryListRequest", () => {
  it("asks for 50 rows when the search names no page size", () => {
    expect(repositoryListRequest({ nameContains: "core" })).toEqual({
      nameContains: "core",
      pageSize: 50,
    });
  });

  it("sends the search's own page size", () => {
    expect(repositoryListRequest({ pageSize: 100, offset: 100 })).toEqual({
      pageSize: 100,
      offset: 100,
    });
  });
});
