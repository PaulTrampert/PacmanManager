/**
 * Where the web host proxies RepoHost: SpaHost's `PacmanManager` API, so RepoHost's `/api/v1/…` is
 * reached at `/api/pacmanmanager/v1/…`. The API client's `baseUrl`.
 */
export const apiBaseUrl = "/api/pacmanmanager";

/** The prefix every path in RepoHost's OpenAPI document starts with, and the proxy replaces. */
export const documentPathPrefix = "/api";

/**
 * The path the API client is called with for a path in RepoHost's OpenAPI document: the document's
 * `/api/v1/Repositories` is the client's `/v1/Repositories`, which {@link apiBaseUrl} turns into
 * `/api/pacmanmanager/v1/Repositories`.
 *
 * @throws {RangeError} when `documentPath` does not start with {@link documentPathPrefix}, since the
 * web host could not reach it.
 */
export function toClientPath(documentPath: string): string {
  if (!documentPath.startsWith(`${documentPathPrefix}/`)) {
    throw new RangeError(
      `"${documentPath}" does not start with "${documentPathPrefix}/", so the web host does not proxy it.`,
    );
  }
  return documentPath.slice(documentPathPrefix.length);
}

/**
 * The OpenAPI document's `paths` object with every key passed through {@link toClientPath}, so the
 * generated types describe the paths the client is called with.
 */
export function toClientPaths<T>(paths: Record<string, T>): Record<string, T> {
  return Object.fromEntries(
    Object.entries(paths).map(([path, item]) => [toClientPath(path), item]),
  );
}
