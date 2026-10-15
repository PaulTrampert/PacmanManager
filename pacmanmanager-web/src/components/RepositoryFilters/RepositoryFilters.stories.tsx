import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { expect, fn, screen, waitFor, within } from "storybook/test";
import type { PublicUserInfo } from "../../api";
import { anArchitecture, aUser } from "../../mocks/fixtures";
import type { Session } from "../../session";
import {
  RepositoryFilters,
  searchDelayMs,
  type RepositoryFiltersProps,
  type RepositoryFilterValues,
} from "./RepositoryFilters";

const me = aUser({
  id: "11111111-1111-1111-1111-111111111111",
  displayName: "Paul Trampert",
});
const alex = aUser({
  id: "22222222-2222-2222-2222-222222222222",
  displayName: "Alex Morgan",
  email: "alex@example.com",
});
const sam = aUser({
  id: "33333333-3333-3333-3333-333333333333",
  displayName: "Sam Lee",
  email: "sam@example.com",
});
const everyone: PublicUserInfo[] = [alex, me, sam];

const signedIn: Session = { status: "signedIn", user: me };
const anonymous: Session = { status: "anonymous" };

const x86_64 = anArchitecture();
const aarch64 = anArchitecture({
  value: "aarch64",
  displayValue: "64-bit ARM (aarch64)",
});

/**
 * A {@link RepositoryFilters} that applies what it reports and answers its users search from a
 * fixed list, as the list page will with its search params and `userListQuery`.
 */
function StatefulRepositoryFilters({
  users: { results, ...users },
  ...props
}: RepositoryFiltersProps) {
  const [filters, setFilters] = useState<RepositoryFilterValues>(props.filters);
  const [found, setFound] = useState(results);
  return (
    <RepositoryFilters
      {...props}
      filters={filters}
      onChange={(change) => {
        setFilters({ ...filters, ...change });
        props.onChange(change);
      }}
      users={{
        ...users,
        results: found,
        onSearch: (text) => {
          setFound(
            results.filter((user) =>
              user.displayName.toLowerCase().includes(text.toLowerCase()),
            ),
          );
          users.onSearch(text);
        },
      }}
    />
  );
}

const meta = {
  component: RepositoryFilters,
  args: {
    filters: {},
    onChange: fn(),
    session: signedIn,
    architectures: [x86_64],
    users: { results: everyone, total: everyone.length, onSearch: fn() },
  },
  render: (args) => <StatefulRepositoryFilters {...args} />,
} satisfies Meta<typeof RepositoryFilters>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Wireframe: list-signed-in. Every filter reads All; one architecture disables its filter. */
export const SignedIn: Story = {
  play: async ({ args, canvas, userEvent }) => {
    const name = canvas.getByRole("textbox", { name: "Name" });
    await expect(name).toHaveAttribute("placeholder", "Search by name");
    await expect(
      canvas.getByRole("combobox", { name: "Visibility" }),
    ).toHaveTextContent("All");
    await expect(canvas.getByRole("combobox", { name: "Owner" })).toBeEnabled();
    const architecture = canvas.getByRole("combobox", {
      name: "Architecture",
    });
    await expect(architecture).toHaveAttribute("aria-disabled", "true");
    await expect(architecture).toHaveTextContent("All");

    // The name search reports once typing stops, not per keystroke.
    await userEvent.type(name, "core");
    await expect(args.onChange).not.toHaveBeenCalled();
    await waitFor(() => expect(args.onChange).toHaveBeenCalledOnce());
    await expect(args.onChange).toHaveBeenLastCalledWith({
      nameContains: "core",
    });
    await new Promise((resolve) => setTimeout(resolve, searchDelayMs + 200));
    await expect(args.onChange).toHaveBeenCalledOnce();

    await userEvent.click(canvas.getByRole("combobox", { name: "Visibility" }));
    await userEvent.click(screen.getByRole("option", { name: "Private" }));
    await expect(args.onChange).toHaveBeenLastCalledWith({ isPublic: false });
    await userEvent.click(canvas.getByRole("combobox", { name: "Visibility" }));
    await userEvent.click(screen.getByRole("option", { name: "Public" }));
    await expect(args.onChange).toHaveBeenLastCalledWith({ isPublic: true });
    await userEvent.click(canvas.getByRole("combobox", { name: "Visibility" }));
    await userEvent.click(screen.getByRole("option", { name: "All" }));
    await expect(args.onChange).toHaveBeenLastCalledWith({
      isPublic: undefined,
    });
  },
};

/** Wireframe: list-signed-in, on a deployment with several architectures. */
export const SeveralArchitectures: Story = {
  args: { architectures: [x86_64, aarch64] },
  play: async ({ args, canvas, userEvent }) => {
    const architecture = canvas.getByRole("combobox", {
      name: "Architecture",
    });
    await userEvent.click(architecture);
    await userEvent.click(
      screen.getByRole("option", { name: "64-bit ARM (aarch64)" }),
    );
    await expect(args.onChange).toHaveBeenLastCalledWith({
      architecture: "aarch64",
    });
    await userEvent.click(architecture);
    await userEvent.click(screen.getByRole("option", { name: "All" }));
    await expect(args.onChange).toHaveBeenLastCalledWith({
      architecture: undefined,
    });
  },
};

/** Wireframe: list-anonymous. Visibility is fixed on Public, and nobody is pinned as "(you)". */
export const Anonymous: Story = {
  args: { session: anonymous, filters: { isPublic: false } },
  play: async ({ args, canvas, userEvent }) => {
    const visibility = canvas.getByRole("combobox", { name: "Visibility" });
    await expect(visibility).toHaveAttribute("aria-disabled", "true");
    await expect(visibility).toHaveTextContent("Public");

    await userEvent.click(canvas.getByRole("combobox", { name: "Owner" }));
    const listbox = await screen.findByRole("listbox");
    await expect(
      within(listbox)
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["Alex Morgan", "Paul Trampert", "Sam Lee"]);
    await expect(args.onChange).not.toHaveBeenCalled();
  },
};

/** Wireframe: list-own-repositories. Filtered by the signed-in user's own id. */
export const OwnRepositories: Story = {
  args: { filters: { ownerId: me.id } },
  play: async ({ args, canvas, userEvent }) => {
    const owner = canvas.getByRole("combobox", { name: "Owner" });
    await expect(owner).toHaveValue("Paul Trampert (you)");

    await userEvent.click(owner);
    await userEvent.click(canvas.getByRole("button", { name: "Clear" }));
    await expect(args.onChange).toHaveBeenLastCalledWith({
      ownerId: undefined,
    });
  },
};

/** Filtered by another user, whose record the caller fetched to label the filter. */
export const OtherOwner: Story = {
  args: { filters: { ownerId: alex.id }, owner: alex },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("combobox", { name: "Owner" })).toHaveValue(
      "Alex Morgan",
    );
  },
};

/** Wireframe: list-single-user. With one user, the owner filter is disabled on All. */
export const SingleUser: Story = {
  args: {
    filters: { ownerId: me.id },
    users: { results: [me], total: 1, onSearch: fn() },
  },
  play: async ({ args, canvas }) => {
    const owner = canvas.getByRole("combobox", { name: "Owner" });
    await expect(owner).toBeDisabled();
    await expect(owner).toHaveValue("");
    await expect(owner).toHaveAttribute("placeholder", "All");
    await expect(args.onChange).not.toHaveBeenCalled();
  },
};

/** Wireframe: list-owner-dropdown-open. The signed-in user comes first, once, then the rest. */
export const OwnerDropdownOpen: Story = {
  play: async ({ args, canvas, userEvent }) => {
    const owner = canvas.getByRole("combobox", { name: "Owner" });
    await userEvent.click(owner);

    const listbox = await screen.findByRole("listbox");
    await expect(
      within(listbox)
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["Paul Trampert (you)", "Alex Morgan", "Sam Lee"]);
    await expect(Array.from(listbox.children)[1]).toHaveAttribute(
      "role",
      "separator",
    );

    await userEvent.click(
      within(listbox).getByRole("option", { name: "Paul Trampert (you)" }),
    );
    await expect(args.onChange).toHaveBeenLastCalledWith({ ownerId: me.id });
    await expect(owner).toHaveValue("Paul Trampert (you)");

    // Typing searches the users once typing stops, and choosing one reports their id.
    await userEvent.clear(owner);
    await userEvent.type(owner, "sam");
    await waitFor(() =>
      expect(args.users.onSearch).toHaveBeenLastCalledWith("sam"),
    );
    await userEvent.click(
      await screen.findByRole("option", { name: "Sam Lee" }),
    );
    await expect(args.onChange).toHaveBeenLastCalledWith({ ownerId: sam.id });
  },
};
