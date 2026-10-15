import Link from "@mui/material/Link";
import Typography from "@mui/material/Typography";
import { Link as RouterLink } from "@tanstack/react-router";
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
): PaginatedTableColumn<Repository, RepositorySortField>[] {
  return [
    {
      id: "name",
      header: "Name",
      sortField: "Name",
      render: (repository) => (
        <Link
          component={RouterLink}
          // The repository page's route is not yet in the route tree, so its path cannot be checked
          // with `params`; an id is a UUID, which needs no escaping.
          to={`/repositories/${repository.id}`}
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
  onSortChange,
  onPaginationChange,
}: RepositoryTableProps) {
  return (
    <PaginatedTable
      aria-label="Repositories"
      columns={repositoryColumns(architectures)}
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
