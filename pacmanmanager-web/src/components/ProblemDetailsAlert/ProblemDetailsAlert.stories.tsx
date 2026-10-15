import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn } from "storybook/test";
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

/** No close button unless the alert is given `onClose`. */
export const NotDismissible: Story = {
  args: {
    error: new ApiError(500, { title: "An error occurred on the server." }),
  },
  play: async ({ canvas }) => {
    await expect(
      canvas.queryByRole("button", { name: "Close" }),
    ).not.toBeInTheDocument();
  },
};

/** A title of the page's own in place of the error's, the detail beneath it, and a close button. */
export const TitledAndDismissible: Story = {
  args: {
    error: new ApiError(500, {
      title: "An error occurred on the server.",
      detail: "The server couldn't be reached. Try again.",
    }),
    title: "core-extras was not deleted",
    onClose: fn(),
  },
  play: async ({ args, canvas, userEvent }) => {
    const alert = canvas.getByRole("alert");
    await expect(alert).toHaveTextContent("core-extras was not deleted");
    await expect(alert).toHaveTextContent(
      "The server couldn't be reached. Try again.",
    );
    await expect(alert).not.toHaveTextContent(
      "An error occurred on the server.",
    );

    await userEvent.click(canvas.getByRole("button", { name: "Close" }));
    await expect(args.onClose).toHaveBeenCalledOnce();
  },
};
