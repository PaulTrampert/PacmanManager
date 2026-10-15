import MuiLink from "@mui/material/Link";
import Typography from "@mui/material/Typography";
import { createLink, type LinkOptions } from "@tanstack/react-router";
import type {
  Architecture,
  Repository,
  RepositoryPaginatedResponse,
  RepositorySortField,
} from "../../api";
import { displayArchitectures } from "../../displayArchitectures";
import { formatDate } from "../../formatDate";
import {
  PaginatedTable,
  type PaginatedTableColumn,
  type Pagination,
  type RequestedSort,
} from "../PaginatedTable";
import { VisibilityChip } from "../VisibilityChip";

/** MUI's link, navigating through the router. */
const Link = createLink(MuiLink);

/** The props {@link RepositoryTable} takes. */
export interface RepositoryTableProps {
  /**
   * The page of repositories to show: the latest response, or the previous one while the next
   * loads. Absent before the first response has arrived.
   */
  response: RepositoryPaginatedResponse | undefined;
  /** Whether a page is being fetched; the rows shown are dimmed under a spinner meanwhile. */
  loading: boolean;
  /** The number of rows a page holds. */
  pageSize: number;
  /** The architectures a repository may support, by which each repository's are named. */
  architectures: readonly Architecture[];
  /** Where a row's name links to: the repository's page, checked against the app's routes. */
  repositoryLink: (repository: Repository) => LinkOptions;
  /** Called with the sort a header click asks for. */
  onSortChange: (sort: RequestedSort<RepositorySortField>) => void;
  /** Called with the page asked for. */
  onPaginationChange: (pagination: Pagination) => void;
}

/** A date-time from the API in a `time` element, in the secondary text colour. */
function DateCell({ value }: { value: string | undefined }) {
  return (
    value !== undefined && (
      <Typography
        component="time"
        dateTime={value}
        variant="body2"
        sx={{ color: "text.secondary" }}
      >
        {formatDate(value)}
      </Typography>
    )
  );
}

function repositoryColumns(
  architectures: readonly Architecture[],
  repositoryLink: (repository: Repository) => LinkOptions,
): PaginatedTableColumn<Repository, RepositorySortField>[] {
  return [
    {
      id: "name",
      header: "Name",
      sortField: "Name",
      render: (repository) => (
        <Link
          {...repositoryLink(repository)}
          underline="hover"
          sx={{ fontWeight: "fontWeightMedium" }}
        >
          {repository.name}
        </Link>
      ),
    },
    {
      id: "owner",
      header: "Owner",
      render: (repository) => repository.owner?.displayName,
    },
    {
      id: "visibility",
      header: "Visibility",
      render: (repository) => <VisibilityChip isPublic={repository.isPublic} />,
    },
    {
      id: "architectures",
      header: "Architectures",
      render: (repository) =>
        displayArchitectures(
          repository.supportedArchitectures ?? [],
          architectures,
        ).join(", "),
    },
    {
      id: "created",
      header: "Created",
      sortField: "Created",
      render: (repository) => <DateCell value={repository.createdAt} />,
    },
    {
      id: "updated",
      header: "Updated",
      sortField: "Updated",
      render: (repository) => <DateCell value={repository.updatedAt} />,
    },
  ];
}

/**
 * One page of the repository list: name (linking to the repository's page), owner, visibility,
 * architectures, and when it was created and updated. The active sort's header and arrow are the
 * response's applied sort, and paging follows the response's offset and total. It is
 * presentational: sort and page changes are reported for the caller to apply.
 */
export function RepositoryTable({
  response,
  loading,
  pageSize,
  architectures,
  repositoryLink,
  onSortChange,
  onPaginationChange,
}: RepositoryTableProps) {
  return (
    <PaginatedTable
      aria-label="Repositories"
      columns={repositoryColumns(architectures, repositoryLink)}
      rows={response?.results ?? []}
      getRowKey={(repository) => repository.id}
      appliedSort={response?.sort}
      onSortChange={onSortChange}
      offset={response?.offset ?? 0}
      pageSize={pageSize}
      total={response?.total ?? 0}
      onPaginationChange={onPaginationChange}
      loading={loading}
    />
  );
}
