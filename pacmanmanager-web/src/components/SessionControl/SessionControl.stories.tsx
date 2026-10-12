import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn } from "storybook/test";
import { currentUser } from "../../testing/fixtures";
import { SessionControl } from "./SessionControl";

const meta = {
  component: SessionControl,
  args: {
    session: { status: "anonymous" },
    onSignOut: fn(),
  },
} satisfies Meta<typeof SessionControl>;

export default meta;

type Story = StoryObj<typeof meta>;

export const SignedOut: Story = {
  play: async ({ canvas }) => {
    const signIn = await canvas.findByRole("link", { name: "Sign in" });
    // The story's router is at `/`, so signing in returns there.
    await expect(signIn).toHaveAttribute("href", "/login?redirectUri=%2F");
    await expect(
      canvas.queryByRole("button", { name: "Sign out" }),
    ).not.toBeInTheDocument();
  },
};

export const SignedIn: Story = {
  args: {
    session: {
      status: "signedIn",
      user: currentUser({ displayName: "Ada Lovelace" }),
    },
  },
  play: async ({ args, canvas, userEvent }) => {
    await expect(await canvas.findByText("Ada Lovelace")).toBeVisible();
    await expect(
      canvas.queryByRole("link", { name: "Sign in" }),
    ).not.toBeInTheDocument();

    await userEvent.click(canvas.getByRole("button", { name: "Sign out" }));

    await expect(args.onSignOut).toHaveBeenCalledOnce();
  },
};
