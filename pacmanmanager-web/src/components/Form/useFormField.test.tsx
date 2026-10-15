import { act, renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import type { FieldValidator } from "../../api";
import { FormContext, type FormContextValue } from "./FormContext";
import { useFormField, type UseFormFieldOptions } from "./useFormField";

const required: FieldValidator = (value) =>
  value === "" ? ["Name is required."] : [];

function renderField(
  options: Partial<UseFormFieldOptions<string>> = {},
  submitted = false,
) {
  const setFieldFailing = vi.fn<FormContextValue["setFieldFailing"]>();
  const context = { submitted, setFieldFailing };
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
  return { setFieldFailing, ...rendered };
}

describe("useFormField", () => {
  it("shows no error before the field is left", () => {
    const { result } = renderField();

    expect(result.current.errors).toEqual([]);
  });

  it("shows its error once the field is left", () => {
    const { result } = renderField();

    act(() => result.current.onBlur());

    expect(result.current.errors).toEqual(["Name is required."]);
  });

  it("shows its error once the form is submitted, though the field was never left", () => {
    const { result } = renderField({}, true);

    expect(result.current.errors).toEqual(["Name is required."]);
  });

  it("shows nothing for a valid value that has been left", () => {
    const { result } = renderField({ value: "core" });

    act(() => result.current.onBlur());

    expect(result.current.errors).toEqual([]);
  });

  it("follows the value once left", () => {
    const { result, rerender } = renderField({ value: "core" });
    act(() => result.current.onBlur());

    rerender({ value: "" });

    expect(result.current.errors).toEqual(["Name is required."]);
  });

  it("reports to the form whether it fails, whether its errors are shown or not", () => {
    const { setFieldFailing, rerender } = renderField();

    expect(setFieldFailing).toHaveBeenLastCalledWith("name", true);

    rerender({ value: "core" });

    expect(setFieldFailing).toHaveBeenLastCalledWith("name", false);
  });

  it("stops failing the form when it goes away", () => {
    const { setFieldFailing, unmount } = renderField();

    unmount();

    expect(setFieldFailing).toHaveBeenLastCalledWith("name", false);
  });

  it("shows the API's error straight away", () => {
    const { result } = renderField({
      value: "core",
      serverError: "A repository with this name already exists.",
    });

    expect(result.current.errors).toEqual([
      "A repository with this name already exists.",
    ]);
  });

  it("shows its own error in place of the API's", () => {
    const { result } = renderField({
      serverError: "A repository with this name already exists.",
    });

    act(() => result.current.onBlur());

    expect(result.current.errors).toEqual(["Name is required."]);
  });

  it("does not block submitting over the API's error", () => {
    const { setFieldFailing } = renderField({
      value: "core",
      serverError: "A repository with this name already exists.",
    });

    expect(setFieldFailing).toHaveBeenLastCalledWith("name", false);
  });

  it("validates a value that is not a string", () => {
    const setFieldFailing = vi.fn<FormContextValue["setFieldFailing"]>();
    const wrapper = ({ children }: { children: ReactNode }) => (
      <FormContext value={{ submitted: true, setFieldFailing }}>
        {children}
      </FormContext>
    );
    const atLeastOne: FieldValidator<string[]> = (value) =>
      value.length === 0 ? ["Choose an architecture."] : [];

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

    expect(result.current.errors).toEqual(["Choose an architecture."]);
    expect(setFieldFailing).toHaveBeenLastCalledWith("architectures", true);
  });

  it("shows every message its validator gives", () => {
    const { result } = renderField(
      {
        value: "-core",
        validate: () => ["Name is too long.", "Name is not valid."],
        serverError: "A repository with this name already exists.",
      },
      true,
    );

    expect(result.current.errors).toEqual([
      "Name is too long.",
      "Name is not valid.",
    ]);
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
