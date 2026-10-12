import { RouterProvider } from "@tanstack/react-router";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "./AppProviders";
import { createQueryClient } from "./queryClient";
import { createAppRouter } from "./router";

const queryClient = createQueryClient();
const router = createAppRouter({ queryClient });

const rootElement = document.getElementById("root");
if (!rootElement) {
  throw new Error("index.html has no #root element to render into");
}

createRoot(rootElement).render(
  <StrictMode>
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
