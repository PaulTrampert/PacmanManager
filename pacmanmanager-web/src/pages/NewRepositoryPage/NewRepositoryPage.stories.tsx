import type { Meta, StoryObj } from "@storybook/react-vite";
import { HttpResponse } from "msw";
import { expect, fn, userEvent, waitFor, within } from "storybook/test";
import { apiHandler } from "../../mocks/api";
import { anArchitecture, aRepository } from "../../mocks/fixtures";
import { NewRepositoryPage } from "./NewRepositoryPage";

const x86_64 = anArchitecture({
  value: "x86_64",
  displayValue: "64-bit Intel/AMD (x86_64)",
});

const created = aRepository({
  id: "22222222-2222-4222-8222-222222222222",
  name: "games",
  isPublic: true,
  supportedArchitectures: ["x86_64"],
});

const meta = {
  component: NewRepositoryPage,
  args: {
    // The story's router has only `/`.
    repositoriesLink: { to: "/" },
    onCreated: fn(),
    onCancel: fn(),
  },
  beforeEach: ({ msw }) => {
    msw.use(
      apiHandler("get", "/v1/Architectures", () => HttpResponse.json([x86_64])),
    );
  },
} satisfies Meta<typeof NewRepositoryPage>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The page as it opens: breadcrumbs, heading, and the form empty (new-empty). */
export const Empty: Story = {
  play: async ({ canvas }) => {
    const trail = await canvas.findByRole("navigation", { name: "breadcrumb" });
    await expect(
      within(trail).getByRole("link", { name: "Repositories" }),
    ).toHaveAttribute("href", "/");
    await expect(within(trail).getByText("New repository")).toHaveAttribute(
      "aria-current",
      "page",
    );
    await expect(
      canvas.getByRole("heading", { level: 1, name: "New repository" }),
    ).toBeVisible();

    await expect(canvas.getByRole("textbox", { name: "Name" })).toHaveValue("");
    await expect(canvas.getByRole("radio", { name: "Private" })).toBeChecked();
    await expect(
      canvas.getByRole("radio", { name: "Public" }),
    ).not.toBeChecked();
    const architectures = canvas.getByRole("combobox", {
      name: "Architectures",
    });
    await expect(architectures).toHaveTextContent("64-bit Intel/AMD (x86_64)");
    await expect(architectures).toHaveAttribute("aria-disabled", "true");
    await expect(canvas.getByRole("button", { name: "Create" })).toBeEnabled();
    await expect(canvas.getByRole("button", { name: "Cancel" })).toBeEnabled();
  },
};

/**
 * A filled-in form submitted (new-filled-in): it sends the name, the visibility and every
 * architecture, and hands the created repository to `onCreated`.
 */
export const Created: Story = {
  play: async ({ canvas, args, msw }) => {
    const bodies: unknown[] = [];
    msw.use(
      apiHandler("post", "/v1/Repositories", async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(created, { status: 201 });
      }),
    );

    await userEvent.type(
      await canvas.findByRole("textbox", { name: "Name" }),
      "games",
    );
    await userEvent.click(canvas.getByRole("radio", { name: "Public" }));
    await userEvent.click(canvas.getByRole("button", { name: "Create" }));

    await waitFor(() => expect(args.onCreated).toHaveBeenCalledWith(created));
    await expect(bodies).toEqual([
      { name: "games", isPublic: true, supportedArchitectures: ["x86_64"] },
    ]);
    await expect(args.onCancel).not.toHaveBeenCalled();
  },
};

/**
 * The create still in flight (new-submitting): the button reads **Creating…** and is disabled,
 * and nothing has been created yet.
 */
export const Submitting: Story = {
  play: async ({ canvas, args, msw }) => {
    msw.use(
      apiHandler("post", "/v1/Repositories", () => new Promise(() => {})),
    );

    await userEvent.type(
      await canvas.findByRole("textbox", { name: "Name" }),
      "games",
    );
    await userEvent.click(canvas.getByRole("button", { name: "Create" }));

    await expect(
      await canvas.findByRole("button", { name: "Creating…" }),
    ).toBeDisabled();
    await expect(args.onCreated).not.toHaveBeenCalled();
  },
};

/**
 * A name another repository already has (new-name-taken): the `409` shows on the name field, not
 * in an alert, and the page stays where it is.
 */
export const NameTaken: Story = {
  play: async ({ canvas, args, msw }) => {
    msw.use(
      apiHandler("post", "/v1/Repositories", () =>
        HttpResponse.json(
          {
            title: "Conflict",
            status: 409,
            detail: "A repository named games already exists.",
          },
          { status: 409 },
        ),
      ),
    );

    const name = await canvas.findByRole("textbox", { name: "Name" });
    await userEvent.type(name, "games");
    await userEvent.click(canvas.getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(name).toHaveAccessibleDescription(
        "A repository with this name already exists.",
      ),
    );
    await expect(name).toBeInvalid();
    await expect(canvas.queryByRole("alert")).not.toBeInTheDocument();
    await expect(canvas.getByRole("button", { name: "Create" })).toBeEnabled();
    await expect(args.onCreated).not.toHaveBeenCalled();
  },
};

/** **Cancel** leaves through `onCancel` without sending anything. */
export const Cancelled: Story = {
  play: async ({ canvas, args }) => {
    await userEvent.click(
      await canvas.findByRole("button", { name: "Cancel" }),
    );

    await expect(args.onCancel).toHaveBeenCalledOnce();
    await expect(args.onCreated).not.toHaveBeenCalled();
  },
};
