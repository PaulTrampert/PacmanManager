// Writes src/api/validation.gen.ts, the request bodies' validation rules, from RepoHost's committed
// OpenAPI document. Run by `npm run generate`; see docs/web-ui-repositories/web-ui-repositories.md,
// "Validation rules from the OpenAPI document".
//
// A pattern the translator does not fully recognise is generated server-only, with a warning. It
// never fails generation: the API checks it, and its `400` puts the API's message on the field.
import { readFile, writeFile } from "node:fs/promises";
import {
  extractValidationRules,
  renderValidationModule,
  type ValidationDocument,
} from "../src/api/validationRules.ts";

const documentUrl = new URL("../openapi/v1.json", import.meta.url);
const rulesUrl = new URL("../src/api/validation.gen.ts", import.meta.url);

const document = JSON.parse(
  await readFile(documentUrl, "utf8"),
) as ValidationDocument;
const { rules, warnings } = extractValidationRules(document);

for (const warning of warnings) {
  console.warn(`generate-validation: ${warning}`);
}
await writeFile(rulesUrl, renderValidationModule(rules));
