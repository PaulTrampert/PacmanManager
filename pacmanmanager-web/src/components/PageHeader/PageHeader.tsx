import Box from "@mui/material/Box";
import Breadcrumbs from "@mui/material/Breadcrumbs";
import MuiLink from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { createLink, type LinkOptions } from "@tanstack/react-router";
import type { ReactNode } from "react";

/** MUI's link, navigating through the router. */
const Link = createLink(MuiLink);

/** A crumb that links to the page it names. */
export interface PageHeaderLink {
  /** The crumb's text. */
  label: string;
  /** Where the crumb links to, checked against the app's routes. */
  link: LinkOptions;
}

/**
 * The trail from the top of the app to the current page: a link for each page above it, then the
 * current page's own label, which is not a link.
 */
export type PageHeaderBreadcrumbs = readonly [...PageHeaderLink[], string];

/** The props {@link PageHeader} takes. */
export interface PageHeaderProps {
  /** The page's heading, rendered as its `h1`. */
  title: ReactNode;
  /** The breadcrumbs above the heading; none when omitted. */
  breadcrumbs?: PageHeaderBreadcrumbs;
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
  const links = breadcrumbs?.slice(0, -1) as PageHeaderLink[] | undefined;
  const current = breadcrumbs?.at(-1) as string | undefined;
  return (
    <Box component="header" sx={{ mb: 2 }}>
      {breadcrumbs && (
        <Breadcrumbs aria-label="breadcrumb" sx={{ mb: 1 }}>
          {links?.map(({ label, link }) => (
            <Link key={label} {...link} underline="hover" color="inherit">
              {label}
            </Link>
          ))}
          <Typography color="text.primary" aria-current="page">
            {current}
          </Typography>
        </Breadcrumbs>
      )}
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
