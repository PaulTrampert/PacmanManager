import type { Decorator, Preview } from "@storybook/react-vite";
import { setupWorker } from "msw/browser";
import { mswLoader } from "msw-storybook-addon/csf3";
import { useState, type ReactNode } from "react";
import { AppProviders } from "../src/AppProviders";
import { handlers } from "../src/mocks/handlers";
import { createQueryClient } from "../src/queryClient";
import { StoryRouter } from "../src/testing/StoryRouter";
import { isWebHostPath } from "../src/webHostPaths";

function StoryProviders({ children }: { children: ReactNode }) {
  // A query client per story, so no story sees another's cache.
  const [queryClient] = useState(createQueryClient);
  return (
    <AppProviders queryClient={queryClient}>
      <StoryRouter>{children}</StoryRouter>
    </AppProviders>
  );
}

const withProviders: Decorator = (Story) => (
  <StoryProviders>
    <Story />
  </StoryProviders>
);

const preview: Preview = {
  loaders: [
    mswLoader(async () => {
      const worker = setupWorker(...handlers);
      await worker.start({
        quiet: true,
        // Storybook's own modules and assets pass through; an unmocked call to the web host is an
        // error, as it is in the unit tests.
        onUnhandledRequest(request, print) {
          if (isWebHostPath(new URL(request.url).pathname)) {
            print.error();
          }
        },
      });
      return worker;
    }),
  ],
  decorators: [withProviders],
};

export default preview;
