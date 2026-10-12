import Typography from "@mui/material/Typography";
import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/")({
  component: IndexPage,
});

function IndexPage() {
  return (
    <>
      <Typography variant="h4" component="h1" gutterBottom>
        Welcome to PacmanManager
      </Typography>
      <Typography>Private pacman repositories for Arch Linux.</Typography>
    </>
  );
}
