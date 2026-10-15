import Typography from "@mui/material/Typography";
import { createFileRoute } from "@tanstack/react-router";

// A stub, so that any page can link here with a typed link. Step 32 of the Web UI: Repositories plan
// replaces it with the repository list page.
export const Route = createFileRoute("/repositories/")({
  component: RepositoryListPage,
});

function RepositoryListPage() {
  return (
    <Typography variant="h4" component="h1">
      Repositories
    </Typography>
  );
}
