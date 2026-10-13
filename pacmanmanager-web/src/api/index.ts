import type { components } from "./schema.gen";

export { client } from "./client";
export { unwrap, type ClientResult } from "./unwrap";
export {
  ApiError,
  isProblemDetails,
  isValidationProblemDetails,
  type ProblemDetails,
  type ValidationProblemDetails,
} from "./ApiError";
export { AntiforgeryError } from "./antiforgery";
export {
  fieldValidator,
  type FieldValidator,
  type FieldValidatorOptions,
  type ValidatedProperty,
  type ValidatedSchema,
} from "./fieldValidator";
export type { paths } from "./schema.gen";

type Schemas = components["schemas"];

// The API's types under friendly names. The app imports these, and never declares its own type for
// anything the OpenAPI document describes.
export type AccessToken = Schemas["AccessToken"];
export type AccessTokenPaginatedResponse =
  Schemas["AccessTokenPaginatedResponse"];
export type AccessTokenSortField = Schemas["AccessTokenSortField"];
export type CreateAccessTokenRequest = Schemas["CreateAccessTokenRequest"];
export type CreatedAccessToken = Schemas["CreatedAccessToken"];
export type CurrentUser = Schemas["CurrentUser"];
export type Package = Schemas["Package"];
export type PackagePaginatedResponse = Schemas["PackagePaginatedResponse"];
export type PackageSortField = Schemas["PackageSortField"];
export type PublicUserInfo = Schemas["PublicUserInfo"];
export type PublicUserInfoPaginatedResponse =
  Schemas["PublicUserInfoPaginatedResponse"];
export type Repository = Schemas["Repository"];
export type RepositoryPaginatedResponse =
  Schemas["RepositoryPaginatedResponse"];
export type RepositorySortField = Schemas["RepositorySortField"];
export type SortDirection = Schemas["SortDirection"];
export type UserSortField = Schemas["UserSortField"];
export type WriteRepositoryRequest = Schemas["WriteRepositoryRequest"];
export type WriteUserRequest = Schemas["WriteUserRequest"];
export type WriteUserRequestPatch = Schemas["WriteUserRequestIPatchObject"];
