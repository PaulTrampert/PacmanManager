import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Typography from "@mui/material/Typography";
import { createLink, type LinkOptions } from "@tanstack/react-router";
import type { ReactNode } from "react";
import type { Architecture, Repository } from "../../api";
import { displayArchitectures } from "../../displayArchitectures";
import { formatDate } from "../../formatDate";
import { PageHeader } from "../PageHeader";
import { VisibilityChip } from "../VisibilityChip";

/** MUI's button, navigating through the router. */
const ButtonLink = createLink(Button);

/** The props {@link RepositoryHeader} takes. */
export interface RepositoryHeaderProps {
  /** The repository the page shows. */
  repository: Repository;
  /** The architectures a repository may support, by which the repository's are named. */
  architectures: readonly Architecture[];
  /** Where the **Repositories** breadcrumb links to: the repository list. */
  repositoriesLink: LinkOptions;
  /** Where **Edit** links to: the repository's edit page. */
  editLink: LinkOptions;
  /** Called when **Delete** is clicked, to open the delete dialog. */
  onDelete: () => void;
}

/** One label-over-value detail in the row beneath the title. */
function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Typography
        component="dt"
        variant="caption"
        sx={{ color: "text.secondary" }}
      >
        {label}
      </Typography>
      <Typography component="dd" variant="body2" sx={{ m: 0 }}>
        {children}
      </Typography>
    </div>
  );
}

/** A date-time from the API in a `time` element. */
function DateValue({ value }: { value: string | undefined }) {
  return (
    value !== undefined && <time dateTime={value}>{formatDate(value)}</time>
  );
}

/**
 * The repository page's header: breadcrumbs back to the list, the name with its visibility chip,
 * **Edit** and **Delete** as the repository's `permissions` allow, and one row of details — owner,
 * architectures, created and updated. It is presentational: the page supplies where its links go
 * and what **Delete** does.
 */
export function RepositoryHeader({
  repository,
  architectures,
  repositoriesLink,
  editLink,
  onDelete,
}: RepositoryHeaderProps) {
  const name = repository.name ?? "";
  const { update, delete: canDelete } = repository.permissions;
  return (
    <Box sx={{ mb: 3 }}>
      <PageHeader
        title={name}
        breadcrumbs={[{ label: "Repositories", link: repositoriesLink }, name]}
        adornment={<VisibilityChip isPublic={repository.isPublic} />}
        actions={
          (update || canDelete) && (
            <>
              {update && (
                <ButtonLink {...editLink} variant="outlined">
                  Edit
                </ButtonLink>
              )}
              {canDelete && (
                <Button variant="outlined" color="error" onClick={onDelete}>
                  Delete
                </Button>
              )}
            </>
          )
        }
      />
      <Box
        component="dl"
        sx={{
          display: "flex",
          flexWrap: "wrap",
          columnGap: 6,
          rowGap: 1,
          m: 0,
        }}
      >
        <Detail label="Owner">{repository.owner?.displayName}</Detail>
        <Detail label="Architectures">
          {displayArchitectures(
            repository.supportedArchitectures ?? [],
            architectures,
          ).join(", ")}
        </Detail>
        <Detail label="Created">
          <DateValue value={repository.createdAt} />
        </Detail>
        <Detail label="Updated">
          <DateValue value={repository.updatedAt} />
        </Detail>
      </Box>
    </Box>
  );
}
