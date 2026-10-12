import type { StorybookConfig } from "@storybook/react-vite";

const config: StorybookConfig = {
  stories: ["../src/**/*.stories.@(ts|tsx)"],
  addons: ["@storybook/addon-vitest", "msw-storybook-addon"],
  framework: "@storybook/react-vite",
  // Holds mockServiceWorker.js, which the postinstall script's `msw init` writes here on `npm ci`.
  staticDirs: ["./public"],
};

export default config;
