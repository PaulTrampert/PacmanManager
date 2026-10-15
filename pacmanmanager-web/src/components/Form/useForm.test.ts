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

  it("does not submit while a field fails, but marks the form submitted", () => {
    const { result, onSubmit } = renderForm();

    act(() => result.current.context.setFieldFailing("name", true));
    act(() => result.current.onSubmit(submitEvent()));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(result.current.context.submitted).toBe(true);
  });

  it("submits once the field passes", () => {
    const { result, onSubmit } = renderForm();

    act(() => result.current.context.setFieldFailing("name", true));
    act(() => result.current.context.setFieldFailing("name", false));
    act(() => result.current.onSubmit(submitEvent()));

    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("blocks on any one failing field", () => {
    const { result, onSubmit } = renderForm();

    act(() => {
      result.current.context.setFieldFailing("name", false);
      result.current.context.setFieldFailing("description", true);
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

  it("keeps setFieldFailing stable across renders", () => {
    const { result, rerender } = renderForm();
    const { setFieldFailing } = result.current.context;

    act(() => setFieldFailing("name", true));
    rerender({ pending: true });

    expect(result.current.context.setFieldFailing).toBe(setFieldFailing);
  });

  it("keeps the context's identity until the form is submitted", () => {
    const { result, rerender } = renderForm();
    const { context } = result.current;

    rerender({});
    expect(result.current.context).toBe(context);

    act(() => result.current.onSubmit(submitEvent()));
    expect(result.current.context).not.toBe(context);
    expect(result.current.context.setFieldFailing).toBe(
      context.setFieldFailing,
    );
  });

  it("keeps the context unchanged when a field reports what it already has", () => {
    const { result } = renderForm();
    act(() => result.current.context.setFieldFailing("name", true));
    const { context } = result.current;

    act(() => result.current.context.setFieldFailing("name", true));

    expect(result.current.context).toBe(context);
  });
});
