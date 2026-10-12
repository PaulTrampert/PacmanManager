import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from "@tanstack/react-router";
import { createContext, use, useState, type ReactNode } from "react";

const StoryContent = createContext<ReactNode>(null);

function StoryOutlet() {
  return use(StoryContent);
}

/** The props {@link StoryRouter} takes. */
export interface StoryRouterProps {
  children: ReactNode;
}

/**
 * Renders `children` inside a router of its own, at `/`, so that a component using router links
 * can be rendered on its own in a story. The children are passed through context rather than
 * captured by the route, so they update when the story's args change.
 */
export function StoryRouter({ children }: StoryRouterProps) {
  const [router] = useState(() =>
    createRouter({
      routeTree: createRootRoute({ component: StoryOutlet }),
      history: createMemoryHistory({ initialEntries: ["/"] }),
    }),
  );
  return (
    <StoryContent value={children}>
      <RouterProvider router={router} />
    </StoryContent>
  );
}
