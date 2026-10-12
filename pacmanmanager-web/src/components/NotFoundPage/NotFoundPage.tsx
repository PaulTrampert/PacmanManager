import Link from "@mui/material/Link";
import Typography from "@mui/material/Typography";
import { Link as RouterLink } from "@tanstack/react-router";

/**
 * Shown for an address the app has no page for, and for a resource the API answers `404` for —
 * which includes one the caller may not see, since the API does not tell the two apart.
 */
export function NotFoundPage() {
  return (
    <>
      <Typography variant="h4" component="h1" gutterBottom>
        Page not found
      </Typography>
      <Typography gutterBottom>
        There is nothing here, or you do not have access to it.
      </Typography>
      <Link component={RouterLink} to="/">
        Go to the home page
      </Link>
    </>
  );
}
