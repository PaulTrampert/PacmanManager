import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, spyOn, waitFor, within } from "storybook/test";
import {
  CopyableCodeBlock,
  copyFailedText,
  defaultFeedbackDuration,
} from "./CopyableCodeBlock";

const text = `[core-extras]
SigLevel = Optional TrustAll
Server = https://packages.example.com/pacman/$repo/$arch`;

const meta = {
  component: CopyableCodeBlock,
  args: {
    text,
    description: "Add this section to /etc/pacman.conf, then run pacman -Sy.",
  },
} satisfies Meta<typeof CopyableCodeBlock>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Long enough for the label to have reverted, short of the test's own timeout. */
const revertTimeout = defaultFeedbackDuration + 2000;

/** Replaces `navigator.clipboard.writeText` for the story, so nothing touches the real clipboard. */
function spyOnWriteText(result: () => Promise<void>) {
  const writeText = spyOn(navigator.clipboard, "writeText").mockImplementation(
    result,
  );
  return { writeText, restore: () => writeText.mockRestore() };
}

export const Idle: Story = {
  play: async ({ canvas, canvasElement }) => {
    await expect(
      await canvas.findByRole("button", { name: "Copy" }),
    ).toBeEnabled();
    await expect(canvasElement.querySelector("pre")?.textContent).toBe(text);
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();
  },
};

let copiedSpy: ReturnType<typeof spyOnWriteText> | undefined;

export const Copied: Story = {
  beforeEach: () => {
    copiedSpy = spyOnWriteText(() => Promise.resolve());
    return copiedSpy.restore;
  },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(await canvas.findByRole("button", { name: "Copy" }));

    await expect(
      await canvas.findByRole("button", { name: "Copied" }),
    ).toBeInTheDocument();
    await expect(copiedSpy?.writeText).toHaveBeenCalledOnce();
    await expect(copiedSpy?.writeText).toHaveBeenCalledWith(text);
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();

    await waitFor(
      () => expect(canvas.getByRole("button", { name: "Copy" })).toBeVisible(),
      { timeout: revertTimeout },
    );
  },
};

/** Asserts the failure state: the label, the message, and exactly the block's text selected. */
async function expectCopyFailed(canvas: ReturnType<typeof within>) {
  await expect(
    await canvas.findByRole("button", { name: "Couldn't copy" }),
  ).toBeInTheDocument();
  await expect(canvas.getByRole("alert")).toHaveTextContent(copyFailedText);
  await expect(window.getSelection()?.toString()).toBe(text);
}

export const ClipboardUnavailable: Story = {
  beforeEach: () => {
    // As on a page served over plain `http://` from anywhere but `localhost`.
    Object.defineProperty(navigator, "clipboard", {
      value: undefined,
      configurable: true,
    });
    return () => {
      Reflect.deleteProperty(navigator, "clipboard");
    };
  },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(await canvas.findByRole("button", { name: "Copy" }));
    await expectCopyFailed(canvas);
  },
};

export const WriteRejected: Story = {
  beforeEach: () =>
    spyOnWriteText(() =>
      Promise.reject(
        new DOMException("Write permission denied.", "NotAllowedError"),
      ),
    ).restore,
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(await canvas.findByRole("button", { name: "Copy" }));
    await expectCopyFailed(canvas);
  },
};
