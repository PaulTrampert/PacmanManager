import {
  http,
  HttpResponse,
  type DefaultBodyType,
  type HttpHandler,
  type HttpResponseResolver,
} from "msw";
import { antiforgeryCookie, antiforgeryPath } from "../api/antiforgery";
import { apiBaseUrl } from "../api/apiPaths";
import type { paths } from "../api/schema.gen";

/** The HTTP methods the API's operations use, as `paths` names them. */
export type ApiMethod = "get" | "put" | "post" | "delete" | "patch";

/** The client paths that have an operation for `method`. */
export type ApiPath<M extends ApiMethod> = {
  [P in keyof paths]: paths[P][M] extends { responses: unknown } ? P : never;
}[keyof paths];

type Operation<M extends ApiMethod, P extends ApiPath<M>> = NonNullable<
  paths[P][M]
>;

type Responses<Op> = Op extends { responses: infer R } ? R : never;

type JsonBody<Content> = Content extends {
  content: { "application/json": infer Body };
}
  ? Body
  : null;

/**
 * The body a handler may answer the operation with: the JSON body of any response the document
 * declares for it, or `null` for a response without one (a `204`, say).
 */
export type ApiResponseBody<M extends ApiMethod, P extends ApiPath<M>> = {
  [S in keyof Responses<Operation<M, P>>]: JsonBody<
    Responses<Operation<M, P>>[S]
  >;
}[keyof Responses<Operation<M, P>>] &
  DefaultBodyType;

/** The JSON request body the operation takes, if any. */
export type ApiRequestBody<M extends ApiMethod, P extends ApiPath<M>> =
  Operation<M, P> extends {
    requestBody?: { content: { "application/json": infer Body } };
  }
    ? Body & DefaultBodyType
    : DefaultBodyType;

/** The operation's path parameters, as the strings MSW matches them as. */
export type ApiPathParams<M extends ApiMethod, P extends ApiPath<M>> =
  Operation<M, P> extends { parameters: { path: infer Params } }
    ? { [K in keyof Params]: string }
    : Record<string, never>;

/**
 * The URL pattern MSW matches a client path at: under the API's base URL, with each `{name}` path
 * parameter turned into MSW's `:name`.
 */
export function toMockPath(path: string): string {
  return `${apiBaseUrl}${path.replace(/\{([^}]+)\}/g, ":$1")}`;
}

/**
 * An MSW handler for one API operation, named by its method and client path as the client is
 * called (`"get"`, `"/v1/Repositories/{id}"`). The path parameters, the request body and the
 * response body are all typed from the OpenAPI document, so a handler that no longer matches the
 * API fails the typecheck.
 *
 * @example
 * server.use(
 *   apiHandler("get", "/v1/Repositories/{id}", ({ params }) =>
 *     HttpResponse.json(repository({ id: params.id })),
 *   ),
 * );
 */
export function apiHandler<M extends ApiMethod, P extends ApiPath<M>>(
  method: M,
  path: P,
  resolver: HttpResponseResolver<
    ApiPathParams<M, P>,
    ApiRequestBody<M, P>,
    ApiResponseBody<M, P>
  >,
): HttpHandler {
  // `http[method]` is a union of identical signatures that TypeScript cannot call with a generic
  // resolver; every member accepts it.
  const handle = http[method] as typeof http.get;
  return handle(toMockPath(path), resolver as HttpResponseResolver);
}

/** The antiforgery token {@link antiforgeryHandler} issues unless given another. */
export const mockAntiforgeryToken = "mock-antiforgery-token";

/**
 * Answers `GET /antiforgery` as the web host does, by setting the `XSRF-TOKEN` cookie. The cookie
 * is written to `document.cookie` here as well as sent in `Set-Cookie`, since a mocked response's
 * cookies are not reliably copied there (MSW 3 does not).
 */
export function antiforgeryHandler(token = mockAntiforgeryToken): HttpHandler {
  return http.get(antiforgeryPath, () => {
    const cookie = `${antiforgeryCookie}=${encodeURIComponent(token)}; Path=/`;
    document.cookie = cookie;
    return new HttpResponse(null, {
      status: 200,
      headers: { "Set-Cookie": cookie },
    });
  });
}
