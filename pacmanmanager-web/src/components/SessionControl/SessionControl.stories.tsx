import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, screen } from "storybook/test";
import { aUser } from "../../mocks/fixtures";
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
      user: aUser({ displayName: "Ada Lovelace" }),
    },
  },
  play: async ({ args, canvas, userEvent }) => {
    const trigger = await canvas.findByRole("button", {
      name: "Ada Lovelace",
    });
    await expect(
      canvas.queryByRole("link", { name: "Sign in" }),
    ).not.toBeInTheDocument();
    // The menu stays closed until the display name is clicked.
    await expect(
      screen.queryByRole("menuitem", { name: "Sign out" }),
    ).not.toBeInTheDocument();

    await userEvent.click(trigger);
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Sign out" }),
    );

    await expect(args.onSignOut).toHaveBeenCalledOnce();
  },
};
