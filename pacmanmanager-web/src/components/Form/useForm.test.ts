import { act, renderHook } from "@testing-library/react";
import type { FormEvent } from "react";
import { describe, expect, it, vi } from "vitest";
import { useForm, type UseFormOptions } from "./useForm";

function submitEvent(): FormEvent<HTMLFormElement> {
  return { preventDefault: vi.fn() } as unknown as FormEvent<HTMLFormElement>;
}

function renderForm(options: Partial<UseFormOptions> = {}) {
  const onSubmit = vi.fn();
  const rendered = renderHook((props: Partial<UseFormOptions>) =>
    useForm({
      onSubmit,
      pending: false,
      submitDisabled: false,
      ...options,
      ...props,
    }),
  );
  return { onSubmit, ...rendered };
}

describe("useForm", () => {
  it("submits a form whose fields are all valid, without the browser submitting it", () => {
    const { result, onSubmit } = renderForm();
    const event = submitEvent();

    act(() => result.current.onSubmit(event));

    expect(event.preventDefault).toHaveBeenCalledOnce();
    expect(onSubmit).toHaveBeenCalledOnce();
    expect(result.current.context.submitted).toBe(true);
  });

  it("does not submit while a field has an error, but marks the form submitted", () => {
    const { result, onSubmit } = renderForm();

    act(() => result.current.context.setFieldError("name", "Name is required."));
    act(() => result.current.onSubmit(submitEvent()));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(result.current.context.submitted).toBe(true);
  });

  it("submits once the field's error is cleared", () => {
    const { result, onSubmit } = renderForm();

    act(() => result.current.context.setFieldError("name", "Name is required."));
    act(() => result.current.context.setFieldError("name", undefined));
    act(() => result.current.onSubmit(submitEvent()));

    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("blocks on any one field's error", () => {
    const { result, onSubmit } = renderForm();

    act(() => {
      result.current.context.setFieldError("name", undefined);
      result.current.context.setFieldError("description", "Too long.");
    });
    act(() => result.current.onSubmit(submitEvent()));

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("does nothing while pending", () => {
    const { result, onSubmit } = renderForm({ pending: true });
    const event = submitEvent();

    act(() => result.current.onSubmit(event));

    expect(event.preventDefault).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
    expect(result.current.context.submitted).toBe(false);
    expect(result.current.submitDisabled).toBe(true);
  });

  it("does nothing while submitting is disabled", () => {
    const { result, onSubmit } = renderForm({ submitDisabled: true });

    act(() => result.current.onSubmit(submitEvent()));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(result.current.submitDisabled).toBe(true);
  });

  it("enables submitting when idle", () => {
    const { result } = renderForm();

    expect(result.current.submitDisabled).toBe(false);
    expect(result.current.context.submitted).toBe(false);
  });

  it("keeps setFieldError stable across renders", () => {
    const { result, rerender } = renderForm();
    const { setFieldError } = result.current.context;

    act(() => setFieldError("name", "Name is required."));
    rerender({ pending: true });

    expect(result.current.context.setFieldError).toBe(setFieldError);
  });

  it("keeps the context unchanged when a field reports the error it already has", () => {
    const { result } = renderForm();
    act(() => result.current.context.setFieldError("name", "Name is required."));
    const { context } = result.current;

    act(() => result.current.context.setFieldError("name", "Name is required."));

    expect(result.current.context).toBe(context);
  });
});
