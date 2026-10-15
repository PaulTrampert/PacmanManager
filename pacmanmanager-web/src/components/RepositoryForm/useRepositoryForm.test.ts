import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api";
import { aRepository, anArchitecture } from "../../mocks/fixtures";
import {
  nameTakenMessage,
  useRepositoryForm,
  type UseRepositoryFormOptions,
} from "./useRepositoryForm";

const architectures = [
  anArchitecture({
    value: "x86_64",
    displayValue: "64-bit Intel/AMD (x86_64)",
  }),
  anArchitecture({ value: "aarch64", displayValue: "64-bit ARM (aarch64)" }),
];

const stored = aRepository({
  name: "core-extras",
  isPublic: true,
  supportedArchitectures: ["aarch64", "x86_64"],
});

function renderForm(options: Partial<UseRepositoryFormOptions> = {}) {
  const onSubmit = vi.fn();
  const rendered = renderHook((props: Partial<UseRepositoryFormOptions>) =>
    useRepositoryForm({ architectures, onSubmit, ...options, ...props }),
  );
  return { onSubmit, ...rendered };
}

describe("useRepositoryForm", () => {
  describe("without a repository", () => {
    it("creates, starting empty and private with every architecture", () => {
      const { result } = renderForm();

      expect(result.current.editing).toBe(false);
      expect(result.current.name).toBe("");
      expect(result.current.isPublic).toBe(false);
      expect(result.current.supportedArchitectures).toEqual([
        "x86_64",
        "aarch64",
      ]);
      expect(result.current.submitDisabled).toBe(false);
    });

    it("shows architectures that arrive after it first renders", () => {
      const { result, rerender } = renderForm({ architectures: [] });

      rerender({ architectures });

      expect(result.current.supportedArchitectures).toEqual([
        "x86_64",
        "aarch64",
      ]);
    });

    it("submits the name, visibility and every architecture", () => {
      const { result, onSubmit } = renderForm();

      act(() => result.current.onNameChange("nightly"));
      act(() => result.current.onIsPublicChange(true));
      act(() => result.current.onSubmit());

      expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
        name: "nightly",
        isPublic: true,
        supportedArchitectures: architectures.map(
          (architecture) => architecture.value,
        ),
      });
    });
  });

  it("creates for a repository without an id", () => {
    const { result } = renderForm({
      repository: { ...stored, id: "" },
    });

    expect(result.current.editing).toBe(false);
    expect(result.current.name).toBe("");
  });

  describe("with a repository", () => {
    it("edits, starting from its values, with Save disabled", () => {
      const { result } = renderForm({ repository: stored });

      expect(result.current.editing).toBe(true);
      expect(result.current.name).toBe("core-extras");
      expect(result.current.isPublic).toBe(true);
      expect(result.current.supportedArchitectures).toEqual([
        "aarch64",
        "x86_64",
      ]);
      expect(result.current.submitDisabled).toBe(true);
    });

    it("treats a repository with no visibility as private", () => {
      const { result } = renderForm({
        repository: { ...stored, isPublic: undefined },
      });

      expect(result.current.isPublic).toBe(false);
    });

    it("enables Save while the name or the visibility differs", () => {
      const { result } = renderForm({ repository: stored });

      act(() => result.current.onNameChange("core"));
      expect(result.current.submitDisabled).toBe(false);
      act(() => result.current.onNameChange("core-extras"));
      expect(result.current.submitDisabled).toBe(true);

      act(() => result.current.onIsPublicChange(false));
      expect(result.current.submitDisabled).toBe(false);
      act(() => result.current.onIsPublicChange(true));
      expect(result.current.submitDisabled).toBe(true);
    });

    it("submits the edited values with the existing architectures", () => {
      const { result, onSubmit } = renderForm({ repository: stored });

      act(() => result.current.onNameChange("core"));
      act(() => result.current.onIsPublicChange(false));
      act(() => result.current.onSubmit());

      expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
        name: "core",
        isPublic: false,
        supportedArchitectures: ["aarch64", "x86_64"],
      });
    });
  });

  describe("errors", () => {
    it("shows nothing without an error", () => {
      const { result } = renderForm({ error: null });

      expect(result.current.fieldErrors).toEqual({});
      expect(result.current.alertError).toBeUndefined();
      expect(result.current.alertMessages).toEqual([]);
    });

    it("puts a 409 on the name field, not in the alert", () => {
      const { result } = renderForm({
        error: new ApiError(409, { title: "Conflict" }),
      });

      expect(result.current.fieldErrors).toEqual({ name: nameTakenMessage });
      expect(result.current.alertError).toBeUndefined();
    });

    it("puts a validation 400's entries on their fields", () => {
      const { result } = renderForm({
        error: new ApiError(400, {
          title: "One or more validation errors occurred.",
          errors: {
            Name: ["The name is not valid."],
            SupportedArchitectures: ["sparc is not supported."],
          },
        }),
      });

      expect(result.current.fieldErrors).toEqual({
        name: "The name is not valid.",
        supportedArchitectures: "sparc is not supported.",
      });
      expect(result.current.alertError).toBeUndefined();
      expect(result.current.alertMessages).toEqual([]);
    });

    it("leaves a validation 400's entries naming no field to the alert", () => {
      const error = new ApiError(400, {
        title: "One or more validation errors occurred.",
        errors: { Name: ["The name is not valid."], Other: ["Bad."] },
      });
      const { result } = renderForm({ error });

      expect(result.current.fieldErrors).toEqual({
        name: "The name is not valid.",
      });
      expect(result.current.alertError).toBe(error);
      expect(result.current.alertMessages).toEqual(["Bad."]);
    });

    it.each([
      ["a 403", new ApiError(403, { title: "Forbidden" })],
      ["a 500", new ApiError(500, { title: "Server error" })],
      ["a network failure", new TypeError("Failed to fetch")],
    ])("leaves %s to the alert", (_, error) => {
      const { result } = renderForm({ error });

      expect(result.current.fieldErrors).toEqual({});
      expect(result.current.alertError).toBe(error);
      expect(result.current.alertMessages).toEqual([]);
    });
  });

  it("keeps its callbacks while nothing they read changes", () => {
    const { result, rerender } = renderForm({ repository: stored });
    const { onNameChange, onIsPublicChange, onSupportedArchitecturesChange } =
      result.current;

    rerender({});

    expect(result.current.onNameChange).toBe(onNameChange);
    expect(result.current.onIsPublicChange).toBe(onIsPublicChange);
    expect(result.current.onSupportedArchitecturesChange).toBe(
      onSupportedArchitecturesChange,
    );
  });
});
