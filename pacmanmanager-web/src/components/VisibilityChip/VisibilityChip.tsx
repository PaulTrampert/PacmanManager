import Chip from "@mui/material/Chip";
import type { Repository } from "../../api";

/** The props {@link VisibilityChip} takes. */
export interface VisibilityChipProps {
  /** Whether the repository is public, as the API's `Repository` carries it. */
  isPublic: Repository["isPublic"];
}

/**
 * A small outlined chip reading **Public** or **Private**, shown in the repository table's
 * Visibility column and beside the name in the repository page's title. Its accessible name names
 * the visibility, so it reads sensibly where no column header gives it context.
 */
export function VisibilityChip({ isPublic }: VisibilityChipProps) {
  const label = isPublic ? "Public" : "Private";
  return (
    <Chip
      role="img"
      aria-label={`Visibility: ${label}`}
      label={label}
      size="small"
      variant="outlined"
      color={isPublic ? "success" : "default"}
    />
  );
}
