import { useState } from "react";
import type { Architecture, paths, PublicUserInfo } from "../../api";
import type { Session } from "../../session";
import { useDebouncedCallback } from "../../useDebouncedCallback";
import type { FilterSelectOption } from "../FilterSelect";

/** How long typing in the name search must stop for before it reports `nameContains`. */
export const searchDelayMs = 300;

type RepositoryListQuery = NonNullable<
  paths["/v1/Repositories"]["get"]["parameters"]["query"]
>;

/** The repository list's filter parameters, as `GET /v1/Repositories` takes them. */
export type RepositoryFilterValues = Pick<
  RepositoryListQuery,
  "nameContains" | "isPublic" | "ownerId" | "architecture"
>;

/**
 * One filter parameter's new value, reported when its control changes: `undefined` when the
 * filter is back to **All**.
 */
export type RepositoryFilterChange = {
  [K in keyof RepositoryFilterValues]-?: {
    [P in K]: RepositoryFilterValues[K];
  };
}[keyof RepositoryFilterValues];

/** The users the owner filter offers, as the caller's search of `GET /v1/Users` found them. */
export interface OwnerSearch {
  /** The users the latest search returned. */
  results: readonly PublicUserInfo[];
  /** How many users there are in all; with one or none the owner filter is disabled. */
  total?: number;
  /** Whether a search is in flight. */
  loading?: boolean;
  /** Called with the text typed into the owner filter, once typing stops. */
  onSearch: (text: string) => void;
}

/** What {@link useRepositoryFilters} needs from `RepositoryFilters`'s props. */
export interface UseRepositoryFiltersOptions {
  /** The filters applied now. */
  filters: RepositoryFilterValues;
  /** Called with one filter parameter whenever its control changes it. */
  onChange: (change: RepositoryFilterChange) => void;
  /** Who is using the app. */
  session: Session;
  /** The architectures a repository may support. */
  architectures: readonly Architecture[];
  /** The user `filters.ownerId` names, once known. */
  owner: PublicUserInfo | undefined;
}

/** The visibility filter's values. */
export type Visibility = "public" | "private";

/** What `RepositoryFilters` renders, as {@link useRepositoryFilters} works it out. */
export interface RepositoryFiltersState {
  /** The name search. */
  name: {
    /** The text in the field, which may be ahead of `nameContains` while typing. */
    text: string;
    /** Handles the field's text changing. */
    onChange: (text: string) => void;
  };
  /** The visibility filter. */
  visibility: {
    value: Visibility | undefined;
    /** Set for an anonymous visitor, who can see public repositories only. */
    disabledValue: Visibility | undefined;
    onChange: (value: Visibility | undefined) => void;
  };
  /** The owner filter. */
  owner: {
    /** The chosen owner, or `undefined` for **All**. */
    value: PublicUserInfo | undefined;
    /** The signed-in user, offered first; none for an anonymous visitor. */
    pinned: readonly PublicUserInfo[];
    /** The text a user is listed and shown as: the signed-in user's ends in "(you)". */
    getOptionLabel: (user: PublicUserInfo) => string;
    onChange: (value: PublicUserInfo | undefined) => void;
  };
  /** The architecture filter. */
  architecture: {
    options: FilterSelectOption<string>[];
    value: string | undefined;
    onChange: (value: string | undefined) => void;
  };
}

/**
 * The behaviour of `RepositoryFilters`.
 *
 * Each control reports its own filter parameter through `onChange`. The name search reports
 * `nameContains` {@link searchDelayMs} after typing stops, and only when it differs from the value
 * last reported; a `nameContains` the caller changes by other means replaces the typed text.
 * Visibility is disabled on **Public** for an anonymous visitor, and the owner filter pins the
 * signed-in user as "(you)".
 */
export function useRepositoryFilters({
  filters,
  onChange,
  session,
  architectures,
  owner,
}: UseRepositoryFiltersOptions): RepositoryFiltersState {
  const [text, setText] = useState(filters.nameContains ?? "");
  // The nameContains the caller last gave, to notice it changing.
  const [given, setGiven] = useState(filters.nameContains);
  // The nameContains the field last reported or took from the caller. The caller changing it to
  // anything else, as clearing the filters does, replaces the typed text; the caller catching up
  // with what was reported does not, however far typing has run on since.
  const [reported, setReported] = useState(filters.nameContains);
  if (filters.nameContains !== given) {
    setGiven(filters.nameContains);
    if (filters.nameContains !== reported) {
      setReported(filters.nameContains);
      setText(filters.nameContains ?? "");
    }
  }

  const reportName = useDebouncedCallback((typed: string) => {
    const nameContains = typed === "" ? undefined : typed;
    if (nameContains !== reported) {
      setReported(nameContains);
      onChange({ nameContains });
    }
  }, searchDelayMs);

  const you: PublicUserInfo | undefined =
    session.status === "signedIn"
      ? { id: session.user.id, displayName: session.user.displayName }
      : undefined;

  let ownerValue: PublicUserInfo | undefined;
  if (filters.ownerId !== undefined) {
    if (filters.ownerId === you?.id) {
      ownerValue = you;
    } else if (filters.ownerId === owner?.id) {
      ownerValue = owner;
    }
  }

  const visibility: Visibility | undefined =
    filters.isPublic === undefined
      ? undefined
      : filters.isPublic
        ? "public"
        : "private";

  return {
    name: {
      text,
      onChange: (typed) => {
        setText(typed);
        reportName(typed);
      },
    },
    visibility: {
      value: visibility,
      disabledValue: session.status === "anonymous" ? "public" : undefined,
      onChange: (value) =>
        onChange({
          isPublic: value === undefined ? undefined : value === "public",
        }),
    },
    owner: {
      value: ownerValue,
      pinned: you === undefined ? [] : [you],
      getOptionLabel: (user) =>
        user.id === you?.id ? `${user.displayName} (you)` : user.displayName,
      onChange: (value) => onChange({ ownerId: value?.id }),
    },
    architecture: {
      options: architectures.map((architecture) => ({
        value: architecture.value,
        label: architecture.displayValue,
      })),
      value: filters.architecture,
      onChange: (value) => onChange({ architecture: value }),
    },
  };
}
