import type { components } from "./schema.gen";

/** An RFC 9457 problem details body, as RepoHost answers an error with. */
export type ProblemDetails = components["schemas"]["ProblemDetails"];

/**
 * The problem details RepoHost answers a request that failed validation with: field names mapped to
 * their messages. The OpenAPI document describes every `400` as plain {@link ProblemDetails}, so
 * this is the one error shape declared here rather than generated.
 */
export type ValidationProblemDetails = ProblemDetails & {
  errors: Record<string, string[]>;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Whether `value` is a problem details body: a JSON object, which every member is optional on. */
export function isProblemDetails(value: unknown): value is ProblemDetails {
  return isRecord(value);
}

/** Whether `problem` carries validation `errors`, each a list of messages. */
export function isValidationProblemDetails(
  problem: unknown,
): problem is ValidationProblemDetails {
  return (
    isProblemDetails(problem) &&
    isRecord(problem.errors) &&
    Object.values(problem.errors).every(
      (messages) =>
        Array.isArray(messages) &&
        messages.every((message) => typeof message === "string"),
    )
  );
}

/**
 * An error response from the API: what query functions and mutations throw in place of the
 * client's result, so components never see an `openapi-fetch` result object.
 */
export class ApiError extends Error {
  /** The response's HTTP status. */
  readonly status: number;

  /** The response's problem details body, or `undefined` when it had none. */
  readonly problem: ProblemDetails | undefined;

  /**
   * @param status the response's HTTP status.
   * @param body the response's parsed body; kept as {@link problem} when it is problem details.
   */
  constructor(status: number, body?: unknown) {
    const problem = isProblemDetails(body) ? body : undefined;
    super(problem?.title ?? `The request failed with status ${status}.`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  /** The problem's short, human-readable summary. */
  get title(): string | undefined {
    return this.problem?.title ?? undefined;
  }

  /** The problem's explanation specific to this occurrence. */
  get detail(): string | undefined {
    return this.problem?.detail ?? undefined;
  }

  /** The validation errors by field, when the response was a validation problem. */
  get errors(): Record<string, string[]> | undefined {
    return isValidationProblemDetails(this.problem)
      ? this.problem.errors
      : undefined;
  }
}
