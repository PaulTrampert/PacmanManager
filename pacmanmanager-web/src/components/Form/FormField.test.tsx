// Type tests of FormField: a field's value, and its control's, follow the property's type in the
// generated schema. Each `@ts-expect-error` is checked by `npm run typecheck`, which fails on an
// unused one, so a case that stopped being a type error would fail it.
import { describe, expect, it } from "vitest";
import type { FormControlProps } from "./FormControlProps";
import { FormField } from "./FormField";

function FormCheckboxField(props: FormControlProps<boolean>) {
  return <input type="checkbox" checked={props.value} readOnly />;
}

function FormListField(props: FormControlProps<string[]>) {
  return <output>{props.value.join(", ")}</output>;
}

describe("FormField", () => {
  it("takes a control of the property's type", () => {
    const fields = [
      <FormField
        key="isPublic"
        schema="WriteRepositoryRequest"
        property="isPublic"
        label="Visibility"
        value={false}
        onChange={(value: boolean) => value}
        control={FormCheckboxField}
      />,
      <FormField
        key="supportedArchitectures"
        schema="WriteRepositoryRequest"
        property="supportedArchitectures"
        label="Architectures"
        value={["x86_64"]}
        onChange={(value: string[]) => value}
        control={FormListField}
      />,
    ];

    expect(fields).toHaveLength(2);
  });

  it("rejects a control of another type", () => {
    const fields = [
      <FormField
        key="name"
        schema="WriteRepositoryRequest"
        property="name"
        label="Name"
        patternMessage="Use letters."
        value=""
        onChange={() => {}}
        // @ts-expect-error -- `name` is a string, not a boolean
        control={FormCheckboxField}
      />,
      <FormField
        key="isPublic"
        schema="WriteRepositoryRequest"
        property="isPublic"
        label="Visibility"
        value={false}
        onChange={() => {}}
        // @ts-expect-error -- `isPublic` is a boolean, not a string[]
        control={FormListField}
      />,
    ];

    expect(fields).toHaveLength(2);
  });

  it("rejects a value of another type", () => {
    const field = (
      <FormField
        schema="WriteRepositoryRequest"
        property="isPublic"
        label="Visibility"
        // @ts-expect-error -- `isPublic` is a boolean
        value="true"
        onChange={() => {}}
        control={FormCheckboxField}
      />
    );

    expect(field).toBeDefined();
  });

  it("needs a control for a property that is not a string", () => {
    const field = (
      // @ts-expect-error -- the default text field edits only a string
      <FormField
        schema="WriteRepositoryRequest"
        property="isPublic"
        label="Visibility"
        value={false}
        onChange={() => {}}
      />
    );

    expect(field).toBeDefined();
  });
});
