import Box from "@mui/material/Box";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { ReactNode } from "react";
import { PageBreadcrumbs, type PageBreadcrumbTrail } from "../PageBreadcrumbs";

/** The props {@link PageHeader} takes. */
export interface PageHeaderProps {
  /** The page's heading, rendered as its `h1`. */
  title: ReactNode;
  /** The breadcrumbs above the heading; none when omitted. */
  breadcrumbs?: PageBreadcrumbTrail;
  /** Shown beside the heading, such as a chip qualifying it. */
  adornment?: ReactNode;
  /** Shown at the right of the heading's row, such as the page's buttons. */
  actions?: ReactNode;
}

/**
 * A page's top: optional breadcrumbs, then the heading with an optional adornment beside it and
 * optional actions aligned to the right.
 */
export function PageHeader({
  title,
  breadcrumbs,
  adornment,
  actions,
}: PageHeaderProps) {
  return (
    <Box component="header" sx={{ mb: 2 }}>
      {breadcrumbs && <PageBreadcrumbs trail={breadcrumbs} sx={{ mb: 1 }} />}
      <Stack
        direction="row"
        useFlexGap
        sx={{ alignItems: "center", flexWrap: "wrap", gap: 2 }}
      >
        <Typography variant="h4" component="h1">
          {title}
        </Typography>
        {adornment}
        {actions && (
          <Stack direction="row" spacing={1} sx={{ ml: "auto" }}>
            {actions}
          </Stack>
        )}
      </Stack>
    </Box>
  );
}
