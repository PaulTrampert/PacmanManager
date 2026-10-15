import { createFileRoute, redirect } from "@tanstack/react-router";

// Repositories are what every visitor comes to see, so the home page is the repository list.
export const Route = createFileRoute("/")({
  beforeLoad: () => {
    throw redirect({ to: "/repositories", replace: true });
  },
});
