import { useSuspenseQuery } from "@tanstack/react-query";
import { sessionQuery, type Session } from "./session";

/**
 * The current session. The root route resolves it before any route renders, so this never
 * suspends inside the app.
 */
export function useSession(): Session {
  return useSuspenseQuery(sessionQuery()).data;
}
