// Writes src/api/schema.gen.ts, the API types, from RepoHost's committed OpenAPI document. Run by
// `npm run generate`; see docs/web-ui-architecture.md, "Types and the client".
//
// The document's paths are RepoHost's own (`/api/v1/…`), but the client calls them through the web
// host's proxy, under its `/api/pacmanmanager` base URL. The types are generated for the paths the
// client is called with (`/v1/…`), so the document's `/api` prefix is dropped first.
import { readFile, writeFile } from "node:fs/promises";
import openapiTS, { astToString, type OpenAPI3 } from "openapi-typescript";
import { toClientPaths } from "../src/api/apiPaths.ts";

const documentUrl = new URL("../openapi/v1.json", import.meta.url);
const schemaUrl = new URL("../src/api/schema.gen.ts", import.meta.url);

const document = JSON.parse(await readFile(documentUrl, "utf8")) as OpenAPI3;
document.paths = toClientPaths(document.paths ?? {});

const banner = `// Generated from openapi/v1.json by scripts/generate-api-schema.ts. Do not edit.\n\n`;
await writeFile(schemaUrl, banner + astToString(await openapiTS(document)));
