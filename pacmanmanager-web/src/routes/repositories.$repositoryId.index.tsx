import Typography from "@mui/material/Typography";
import { createFileRoute } from "@tanstack/react-router";

// A stub, so that any page can link here with a typed link. Step 33 of the Web UI: Repositories plan
// replaces it with the repository page.
export const Route = createFileRoute("/repositories/$repositoryId/")({
  component: RepositoryPage,
});

function RepositoryPage() {
  return (
    <Typography variant="h4" component="h1">
      Repository
    </Typography>
  );
}
