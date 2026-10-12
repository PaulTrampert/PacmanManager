import { setupServer } from "msw/node";
import { handlers } from "./handlers";

/** The MSW server the unit tests run against; started by `src/testing/setup.ts`. */
export const server = setupServer(...handlers);
