import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";
import { ApiError } from "../../api";
import { fallbackErrorTitle, ProblemDetailsAlert } from "./ProblemDetailsAlert";

const meta = {
  component: ProblemDetailsAlert,
} satisfies Meta<typeof ProblemDetailsAlert>;

export default meta;

type Story = StoryObj<typeof meta>;

export const TitleAndDetail: Story = {
  args: {
    error: new ApiError(409, {
      title: "Conflict",
      detail: "A repository named core already exists.",
    }),
  },
  play: async ({ canvas }) => {
    const alert = canvas.getByRole("alert");
    await expect(alert).toHaveTextContent("Conflict");
    await expect(alert).toHaveTextContent(
      "A repository named core already exists.",
    );
  },
};

export const TitleOnly: Story = {
  args: {
    error: new ApiError(500, { title: "An error occurred on the server." }),
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("alert")).toHaveTextContent(
      "An error occurred on the server.",
    );
  },
};

export const NoProblemDetails: Story = {
  args: { error: new ApiError(502) },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("alert")).toHaveTextContent(
      "The request failed with status 502.",
    );
  },
};

export const UnmatchedValidationErrors: Story = {
  args: {
    error: new ApiError(400, {
      title: "One or more validation errors occurred.",
      errors: { Upload: ["The file is not a pacman package."] },
    }),
    messages: ["The file is not a pacman package."],
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("alert")).toHaveTextContent(
      "One or more validation errors occurred.",
    );
    await expect(canvas.getByRole("listitem")).toHaveTextContent(
      "The file is not a pacman package.",
    );
  },
};

export const NetworkFailure: Story = {
  args: { error: new TypeError("Failed to fetch") },
  play: async ({ canvas }) => {
    const alert = canvas.getByRole("alert");
    await expect(alert).toHaveTextContent(fallbackErrorTitle);
    await expect(alert).toHaveTextContent("Failed to fetch");
  },
};
