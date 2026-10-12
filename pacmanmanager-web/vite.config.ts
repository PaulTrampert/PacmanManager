import { storybookTest } from "@storybook/addon-vitest/vitest-plugin";
import { tanstackRouter } from "@tanstack/router-plugin/vite";
import react from "@vitejs/plugin-react";
import { playwright } from "@vitest/browser-playwright";
import { defineConfig } from "vitest/config";
import { webHostPaths } from "./src/webHostPaths.ts";

// The compose stack's web host. The dev server forwards the paths SpaHost answers itself to it, so
// the app's code is the same whether Vite or SpaHost serves it; the app never names a host.
const webHost = "http://localhost:8081";

export default defineConfig({
  // The router plugin must come before the React plugin. Its settings live in tsr.config.json,
  // which `npm run generate` (the router CLI) reads too, so both produce the same route tree.
  plugins: [tanstackRouter(), react()],
  server: {
    port: 3000,
    strictPort: true,
    proxy: Object.fromEntries(
      webHostPaths.map((path) => [
        path,
        { target: webHost, changeOrigin: false },
      ]),
    ),
  },
  test: {
    projects: [
      {
        extends: true,
        test: {
          name: "unit",
          environment: "jsdom",
          include: ["src/**/*.test.{ts,tsx}", "e2e/**/*.test.ts"],
          setupFiles: ["src/testing/setup.ts"],
        },
      },
      {
        extends: true,
        plugins: [storybookTest({ configDir: ".storybook" })],
        test: {
          name: "storybook",
          browser: {
            enabled: true,
            headless: true,
            provider: playwright(),
            instances: [{ browser: "chromium" }],
          },
        },
      },
    ],
  },
});
