import Typography from "@mui/material/Typography";
import { createFileRoute } from "@tanstack/react-router";

// A stub, so that any page can link here with a typed link. Step 35 of the Web UI: Repositories plan
// replaces it with the edit repository page.
export const Route = createFileRoute("/repositories/$repositoryId/edit")({
  component: EditRepositoryPage,
});

function EditRepositoryPage() {
  return (
    <Typography variant="h4" component="h1">
      Edit repository
    </Typography>
  );
}
