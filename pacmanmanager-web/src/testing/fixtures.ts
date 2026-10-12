import type { CurrentUser } from "../api";

/** A signed-in user's own record, with any field overridden. */
export function currentUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: "6f1c1f0e-8a52-4a3b-9a57-2c5d3e8f9b10",
    displayName: "Test User",
    email: "test@example.com",
    ...overrides,
  };
}
