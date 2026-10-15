import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import type { Architecture, PublicUserInfo } from "../../api";
import type { Session } from "../../session";
import { AsyncAutocomplete } from "../AsyncAutocomplete";
import { FilterSelect, type FilterSelectOption } from "../FilterSelect";
import {
  searchDelayMs,
  useRepositoryFilters,
  type OwnerSearch,
  type RepositoryFilterChange,
  type RepositoryFilterValues,
  type Visibility,
} from "./useRepositoryFilters";

export { searchDelayMs };
export type { OwnerSearch, RepositoryFilterChange, RepositoryFilterValues };

const visibilityOptions: FilterSelectOption<Visibility>[] = [
  { value: "public", label: "Public" },
  { value: "private", label: "Private" },
];

/** The props {@link RepositoryFilters} takes. */
export interface RepositoryFiltersProps {
  /** The filters applied now. */
  filters: RepositoryFilterValues;
  /**
   * Called with one filter parameter whenever its control changes it, `undefined` meaning **All**.
   * The name search calls it {@link searchDelayMs} after typing stops.
   */
  onChange: (change: RepositoryFilterChange) => void;
  /** Who is using the app: an anonymous visitor's visibility is fixed on **Public**. */
  session: Session;
  /** The architectures a repository may support, as `GET /v1/Architectures` lists them. */
  architectures: readonly Architecture[];
  /** The users the owner filter offers, from the caller's search. */
  users: OwnerSearch;
  /**
   * The user `filters.ownerId` names, to show in the owner filter. Not needed when that is the
   * signed-in user, and ignored when it is someone else; until it is given, the filter reads **All**.
   */
  owner?: PublicUserInfo;
}

/**
 * The repository list's filter bar: a name search, visibility, owner and architecture.
 *
 * Each control reports its own filter parameter. The owner filter lists the signed-in user first,
 * as "(you)". A filter that cannot narrow anything is disabled on **All**, and an anonymous
 * visitor's visibility is disabled on **Public**. It fetches nothing: the users, the architectures
 * and the session are given to it.
 */
export function RepositoryFilters({
  filters,
  onChange,
  session,
  architectures,
  users,
  owner,
}: RepositoryFiltersProps) {
  const state = useRepositoryFilters({
    filters,
    onChange,
    session,
    architectures,
    owner,
  });

  return (
    <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: "wrap" }}>
      <TextField
        size="small"
        label="Name"
        placeholder="Search by name"
        value={state.name.text}
        onChange={(event) => state.name.onChange(event.target.value)}
        sx={{ width: 240 }}
        slotProps={{ inputLabel: { shrink: true } }}
      />
      <FilterSelect
        label="Visibility"
        options={visibilityOptions}
        value={state.visibility.value}
        disabledValue={state.visibility.disabledValue}
        onChange={state.visibility.onChange}
        sx={{ width: 130 }}
      />
      <AsyncAutocomplete<PublicUserInfo>
        label="Owner"
        value={state.owner.value}
        onChange={state.owner.onChange}
        onSearch={users.onSearch}
        options={users.results}
        pinnedOptions={state.owner.pinned}
        loading={users.loading}
        totalOptions={users.total}
        getOptionKey={(user) => user.id}
        getOptionLabel={state.owner.getOptionLabel}
        getOptionSearchText={(user) => user.displayName}
      />
      <FilterSelect
        label="Architecture"
        options={state.architecture.options}
        value={state.architecture.value}
        onChange={state.architecture.onChange}
        sx={{ width: 200 }}
      />
    </Stack>
  );
}
