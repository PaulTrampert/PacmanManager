import { describe, expect, expectTypeOf, it } from "vitest";
import document from "../../openapi/v1.json";
import type {
  Architecture,
  CurrentUser,
  PublicUserInfoPaginatedResponse,
  Repository,
  RepositoryPaginatedResponse,
} from "../api";
import { aPage, aRepository, aUser, anArchitecture } from "./fixtures";

type SchemaName = keyof typeof document.components.schemas;

function requiredOf(schema: SchemaName): string[] {
  const definition = document.components.schemas[schema];
  return "required" in definition ? definition.required : [];
}

describe.each([
  { schema: "Repository", build: () => aRepository() },
  { schema: "Architecture", build: () => anArchitecture() },
  { schema: "CurrentUser", build: () => aUser() },
] as const)("the default $schema", ({ schema, build }) => {
  it.each(requiredOf(schema))("fills the required %s", (field) => {
    expect(build()).toHaveProperty(field);
    expect((build() as Record<string, unknown>)[field]).not.toBeUndefined();
  });
});

describe("aRepository", () => {
  it("is a Repository", () => {
    expectTypeOf(aRepository()).toEqualTypeOf<Repository>();
  });

  it("is owned by the default user", () => {
    const { id, displayName } = aUser();
    expect(aRepository().owner).toEqual({ id, displayName });
  });

  it("lets overrides win over defaults", () => {
    const owner = {
      id: "2a7c9e1b-3d4f-4a5b-8c6d-7e8f9a0b1c2d",
      displayName: "Other",
    };
    const repository = aRepository({
      name: "other-repo",
      isPublic: false,
      owner,
      permissions: { update: false, delete: false },
    });
    expect(repository).toMatchObject({
      name: "other-repo",
      isPublic: false,
      owner,
      permissions: { update: false, delete: false },
    });
    expect(repository.id).toBe(aRepository().id);
  });
});

describe("anArchitecture", () => {
  it("is an Architecture", () => {
    expectTypeOf(anArchitecture()).toEqualTypeOf<Architecture>();
  });

  it("lets overrides win over defaults", () => {
    expect(
      anArchitecture({
        value: "aarch64",
        displayValue: "ARM 64-bit (aarch64)",
      }),
    ).toEqual({ value: "aarch64", displayValue: "ARM 64-bit (aarch64)" });
  });
});

describe("aUser", () => {
  it("is a CurrentUser", () => {
    expectTypeOf(aUser()).toEqualTypeOf<CurrentUser>();
  });

  it("lets overrides win over defaults", () => {
    const user = aUser({ displayName: "Someone Else" });
    expect(user.displayName).toBe("Someone Else");
    expect(user.email).toBe(aUser().email);
  });
});

describe("aPage", () => {
  it("is the paginated response its results are listed in", () => {
    expectTypeOf(
      aPage([aRepository()]),
    ).toEqualTypeOf<RepositoryPaginatedResponse>();
    expectTypeOf(
      aPage([aUser()]),
    ).toEqualTypeOf<PublicUserInfoPaginatedResponse>();
  });

  it.each([
    { schema: "RepositoryPaginatedResponse", page: aPage([aRepository()]) },
    { schema: "PublicUserInfoPaginatedResponse", page: aPage([aUser()]) },
  ] as const)("fills the required fields of a $schema", ({ schema, page }) => {
    for (const field of requiredOf(schema)) {
      expect(page).toHaveProperty(field);
    }
  });

  it("holds the results from the start of the listing", () => {
    const first = aRepository();
    const second = aRepository({ name: "second" });
    expect(aPage([first, second])).toMatchObject({
      results: [first, second],
      offset: 0,
      total: 2,
    });
  });

  it("reports the API's default sort for repositories", () => {
    expect(aPage([aRepository()]).sort).toEqual({
      sortBy: "Name",
      direction: "Ascending",
    });
  });

  it("reports the API's default sort for users", () => {
    expect(aPage([aUser()]).sort).toEqual({
      sortBy: "DisplayName",
      direction: "Ascending",
    });
  });

  it("counts the items before the page in the default total", () => {
    expect(aPage([aRepository()], { offset: 50 }).total).toBe(51);
  });

  it("lets overrides win over defaults", () => {
    expect(
      aPage([aRepository()], {
        sort: { sortBy: "Created", direction: "Descending" },
        offset: 25,
        total: 120,
      }),
    ).toMatchObject({
      offset: 25,
      total: 120,
      sort: { sortBy: "Created", direction: "Descending" },
    });
  });

  it("takes the sort of a page with no results", () => {
    const page = aPage<Repository>([], {
      sort: { sortBy: "Updated", direction: "Descending" },
    });
    expectTypeOf(page).toEqualTypeOf<RepositoryPaginatedResponse>();
    expect(page).toEqual({
      results: [],
      offset: 0,
      total: 0,
      sort: { sortBy: "Updated", direction: "Descending" },
    });
  });

  it("requires the sort of a page with no results", () => {
    // @ts-expect-error A page with no results cannot tell which listing it is from.
    aPage<Repository>([]);
  });
});
