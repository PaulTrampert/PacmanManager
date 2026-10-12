import js from "@eslint/js";
import pluginQuery from "@tanstack/eslint-plugin-query";
import pluginRouter from "@tanstack/eslint-plugin-router";
import prettier from "eslint-config-prettier/flat";
import reactHooks from "eslint-plugin-react-hooks";
import storybook from "eslint-plugin-storybook";
import { defineConfig, globalIgnores } from "eslint/config";
import globals from "globals";
import tseslint from "typescript-eslint";

export default defineConfig(
  globalIgnores([
    "dist/",
    "storybook-static/",
    "coverage/",
    ".tanstack/",
    "**/*.gen.ts",
    ".storybook/public/",
  ]),
  // Storybook's config directory is a dot-directory, which ESLint skips unless asked.
  { files: ["**/*.{js,ts,tsx}", ".storybook/**/*.{ts,tsx}"] },
  js.configs.recommended,
  tseslint.configs.recommended,
  reactHooks.configs.flat["recommended-latest"],
  pluginQuery.configs["flat/recommended"],
  pluginRouter.configs["flat/recommended"],
  storybook.configs["flat/recommended"],
  {
    languageOptions: {
      globals: { ...globals.browser },
    },
  },
  {
    files: ["*.{js,ts}"],
    languageOptions: {
      globals: { ...globals.node },
    },
  },
  // Last, so that formatting is Prettier's business alone.
  prettier,
);
