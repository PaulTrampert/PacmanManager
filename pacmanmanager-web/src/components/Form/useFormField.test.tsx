import { act, renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import type { FieldValidator } from "../../api";
import { FormContext, type FormContextValue } from "./FormContext";
import { useFormField, type UseFormFieldOptions } from "./useFormField";

const required: FieldValidator = (value) =>
  value === "" ? "Name is required." : undefined;

function renderField(
  options: Partial<UseFormFieldOptions<string>> = {},
  submitted = false,
) {
  const setFieldError = vi.fn<FormContextValue["setFieldError"]>();
  const context = { submitted, setFieldError };
  const wrapper = ({ children }: { children: ReactNode }) => (
    <FormContext value={context}>{children}</FormContext>
  );
  const rendered = renderHook(
    (props: Partial<UseFormFieldOptions<string>>) =>
      useFormField({
        name: "name",
        value: "",
        validate: required,
        serverError: undefined,
        ...options,
        ...props,
      }),
    { wrapper },
  );
  return { setFieldError, ...rendered };
}

describe("useFormField", () => {
  it("shows no error before the field is left", () => {
    const { result } = renderField();

    expect(result.current.error).toBeUndefined();
  });

  it("shows its error once the field is left", () => {
    const { result } = renderField();

    act(() => result.current.onBlur());

    expect(result.current.error).toBe("Name is required.");
  });

  it("shows its error once the form is submitted, though the field was never left", () => {
    const { result } = renderField({}, true);

    expect(result.current.error).toBe("Name is required.");
  });

  it("shows nothing for a valid value that has been left", () => {
    const { result } = renderField({ value: "core" });

    act(() => result.current.onBlur());

    expect(result.current.error).toBeUndefined();
  });

  it("follows the value once left", () => {
    const { result, rerender } = renderField({ value: "core" });
    act(() => result.current.onBlur());

    rerender({ value: "" });

    expect(result.current.error).toBe("Name is required.");
  });

  it("reports its error to the form, whether shown or not", () => {
    const { setFieldError, rerender } = renderField();

    expect(setFieldError).toHaveBeenLastCalledWith("name", "Name is required.");

    rerender({ value: "core" });

    expect(setFieldError).toHaveBeenLastCalledWith("name", undefined);
  });

  it("clears its error from the form when it goes away", () => {
    const { setFieldError, unmount } = renderField();

    unmount();

    expect(setFieldError).toHaveBeenLastCalledWith("name", undefined);
  });

  it("shows the API's error straight away", () => {
    const { result } = renderField({
      value: "core",
      serverError: "A repository with this name already exists.",
    });

    expect(result.current.error).toBe(
      "A repository with this name already exists.",
    );
  });

  it("shows its own error in place of the API's", () => {
    const { result } = renderField({
      serverError: "A repository with this name already exists.",
    });

    act(() => result.current.onBlur());

    expect(result.current.error).toBe("Name is required.");
  });

  it("does not block submitting over the API's error", () => {
    const { setFieldError } = renderField({
      value: "core",
      serverError: "A repository with this name already exists.",
    });

    expect(setFieldError).toHaveBeenLastCalledWith("name", undefined);
  });

  it("validates a value that is not a string", () => {
    const setFieldError = vi.fn<FormContextValue["setFieldError"]>();
    const wrapper = ({ children }: { children: ReactNode }) => (
      <FormContext value={{ submitted: true, setFieldError }}>
        {children}
      </FormContext>
    );
    const atLeastOne: FieldValidator<string[]> = (value) =>
      value.length === 0 ? "Choose an architecture." : undefined;

    const { result } = renderHook(
      () =>
        useFormField({
          name: "architectures",
          value: [] as string[],
          validate: atLeastOne,
          serverError: undefined,
        }),
      { wrapper },
    );

    expect(result.current.error).toBe("Choose an architecture.");
    expect(setFieldError).toHaveBeenLastCalledWith(
      "architectures",
      "Choose an architecture.",
    );
  });

  it("throws outside a form", () => {
    // React logs the error it rethrows; the test is that it throws.
    const consoleError = vi
      .spyOn(console, "error")
      .mockImplementation(() => {});
    try {
      expect(() =>
        renderHook(() =>
          useFormField({
            name: "name",
            value: "",
            validate: required,
            serverError: undefined,
          }),
        ),
      ).toThrow("A form field must be rendered inside a Form.");
    } finally {
      consoleError.mockRestore();
    }
  });
});
