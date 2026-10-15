import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, spyOn, within } from "storybook/test";
import { aRepository } from "../../mocks/fixtures";
import {
  instructionText,
  PacmanConfSection,
  privateNoteText,
} from "./PacmanConfSection";

const serverUrl = "https://packages.example.com/pacman/$repo/$arch";

const meta = {
  component: PacmanConfSection,
  args: {
    repository: aRepository({ name: "core-extras", serverUrl }),
  },
} satisfies Meta<typeof PacmanConfSection>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The section the story's repository should produce, built here independently of the component. */
function sectionFor(name: string) {
  return `[${name}]\nSigLevel = Optional TrustAll\nServer = ${serverUrl}`;
}

/** Asserts the accordion is collapsed, as it is each time it is rendered. */
async function expectCollapsed(canvas: ReturnType<typeof within>) {
  const summary = await canvas.findByRole("button", {
    name: /Use this repository/,
  });
  await expect(summary).toHaveAttribute("aria-expanded", "false");
  await expect(
    canvas.queryByRole("button", { name: "Copy" }),
  ).not.toBeInTheDocument();
  return summary;
}

let writeText: ReturnType<typeof spyOnWriteText>["writeText"] | undefined;

/** Replaces `navigator.clipboard.writeText` for the story, so nothing touches the real clipboard. */
function spyOnWriteText() {
  const spy = spyOn(navigator.clipboard, "writeText").mockResolvedValue();
  return { writeText: spy, restore: () => spy.mockRestore() };
}

/** Spies on `writeText` for one story, restoring it after. */
function beforeEach() {
  const spy = spyOnWriteText();
  writeText = spy.writeText;
  return spy.restore;
}

export const Collapsed: Story = {
  play: async ({ canvas }) => {
    await expectCollapsed(canvas);
    await expect(
      canvas.getByText("The pacman.conf section for this repository"),
    ).toBeVisible();
  },
};

export const ExpandedPublic: Story = {
  beforeEach,
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(await expectCollapsed(canvas));

    await expect(canvas.getByText(instructionText)).toBeVisible();
    await expect(canvas.queryByText(privateNoteText)).not.toBeInTheDocument();

    await userEvent.click(canvas.getByRole("button", { name: "Copy" }));
    await expect(
      await canvas.findByRole("button", { name: "Copied" }),
    ).toBeInTheDocument();
    await expect(writeText).toHaveBeenCalledOnce();
    await expect(writeText).toHaveBeenCalledWith(sectionFor("core-extras"));
  },
};

export const ExpandedPrivate: Story = {
  args: {
    repository: aRepository({
      name: "my-aur-builds",
      isPublic: false,
      serverUrl,
    }),
  },
  beforeEach,
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(await expectCollapsed(canvas));

    await expect(canvas.getByText(privateNoteText)).toBeVisible();

    await userEvent.click(canvas.getByRole("button", { name: "Copy" }));
    await expect(
      await canvas.findByRole("button", { name: "Copied" }),
    ).toBeInTheDocument();
    await expect(writeText).toHaveBeenCalledOnce();
    await expect(writeText).toHaveBeenCalledWith(sectionFor("my-aur-builds"));
  },
};
