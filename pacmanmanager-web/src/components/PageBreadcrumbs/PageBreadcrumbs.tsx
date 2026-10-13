import Breadcrumbs, { type BreadcrumbsProps } from "@mui/material/Breadcrumbs";
import MuiLink from "@mui/material/Link";
import Typography from "@mui/material/Typography";
import { createLink, type LinkOptions } from "@tanstack/react-router";

/** MUI's link, navigating through the router. */
const Link = createLink(MuiLink);

/** A crumb that links to the page it names. */
export interface PageBreadcrumbLink {
  /** The crumb's text. */
  label: string;
  /** Where the crumb links to, checked against the app's routes. */
  link: LinkOptions;
}

/**
 * The trail from the top of the app to the current page: a link for each page above it, then the
 * current page's own label, which is not a link.
 */
export type PageBreadcrumbTrail = readonly [...PageBreadcrumbLink[], string];

/** The props {@link PageBreadcrumbs} takes. */
export interface PageBreadcrumbsProps {
  /** The trail to show. */
  trail: PageBreadcrumbTrail;
  /** Styles for the `nav`, such as the space beneath it. */
  sx?: BreadcrumbsProps["sx"];
}

/**
 * Breadcrumbs for the current page. `PageHeader` shows them above its heading, but a page without
 * a header can use them on their own.
 */
export function PageBreadcrumbs({ trail, sx }: PageBreadcrumbsProps) {
  const links = trail.slice(0, -1) as PageBreadcrumbLink[];
  const current = trail.at(-1) as string;
  return (
    <Breadcrumbs aria-label="breadcrumb" sx={sx}>
      {links.map(({ label, link }) => (
        <Link key={label} {...link} underline="hover" color="inherit">
          {label}
        </Link>
      ))}
      <Typography sx={{ color: "text.primary" }} aria-current="page">
        {current}
      </Typography>
    </Breadcrumbs>
  );
}
