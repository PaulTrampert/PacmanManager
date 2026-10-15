import type { Repository } from "../../api";

/**
 * The `pacman.conf` section that adds `repository` to pacman: its name as stored, in brackets, and
 * its `serverUrl` verbatim as the `Server` line.
 */
export function pacmanConfSectionText({
  name,
  serverUrl,
}: Pick<Repository, "name" | "serverUrl">): string {
  return `[${name}]
SigLevel = Optional TrustAll
Server = ${serverUrl}`;
}
