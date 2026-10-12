import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn } from "storybook/test";
import { ApiError } from "../../api";
import { forbiddenText } from "../ForbiddenMessage";
import { ErrorPage } from "./ErrorPage";

const meta = {
  component: ErrorPage,
  args: { onRetry: fn() },
} satisfies Meta<typeof ErrorPage>;

export default meta;

type Story = StoryObj<typeof meta>;

export const ServerError: Story = {
  args: {
    error: new ApiError(500, {
      title: "An error occurred while processing your request.",
      detail: "The repository database could not be read.",
    }),
  },
  play: async ({ args, canvas, userEvent }) => {
    await expect(
      canvas.getByRole("heading", { level: 1, name: "Something went wrong" }),
    ).toBeVisible();
    const alert = canvas.getByRole("alert");
    await expect(alert).toHaveTextContent(
      "An error occurred while processing your request.",
    );
    await expect(alert).toHaveTextContent(
      "The repository database could not be read.",
    );

    await userEvent.click(canvas.getByRole("button", { name: "Try again" }));

    await expect(args.onRetry).toHaveBeenCalledOnce();
  },
};

export const NetworkFailure: Story = {
  args: { error: new TypeError("Failed to fetch") },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("alert")).toHaveTextContent(
      "Failed to fetch",
    );
    await expect(
      canvas.getByRole("button", { name: "Try again" }),
    ).toBeVisible();
  },
};

export const Forbidden: Story = {
  args: { error: new ApiError(403, { title: "Forbidden" }) },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("alert")).toHaveTextContent(forbiddenText);
    await expect(
      canvas.queryByRole("button", { name: "Try again" }),
    ).not.toBeInTheDocument();
  },
};
