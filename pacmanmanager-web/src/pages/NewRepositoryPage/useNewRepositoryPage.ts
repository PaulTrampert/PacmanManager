import { useSuspenseQuery } from "@tanstack/react-query";
import type {
  Architecture,
  Repository,
  WriteRepositoryRequest,
} from "../../api";
import { architecturesQuery } from "../../queries/architectures";
import { useCreateRepository } from "../../queries/repositories";

/** What {@link useNewRepositoryPage} gives the page to render. */
export interface NewRepositoryPageState {
  /** Every architecture a repository may support, for the form. */
  architectures: readonly Architecture[];
  /** Creates a repository from the form's values, calling `onCreated` with it once it exists. */
  create: (values: WriteRepositoryRequest) => void;
  /** Whether the create request is in flight. */
  pending: boolean;
  /** The create request's error, for the form to show, or `null` before one fails. */
  error: unknown;
}

/**
 * The new repository page's behaviour: it reads the architectures the route's loader ensured, so
 * it never suspends there, and creates the repository the form submits with `useCreateRepository`,
 * handing the created repository to `onCreated`. A failure stays on the page, for the form to show.
 */
export function useNewRepositoryPage(
  onCreated: (repository: Repository) => void,
): NewRepositoryPageState {
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  const createRepository = useCreateRepository();
  const create = (values: WriteRepositoryRequest) => {
    createRepository.mutate(values, { onSuccess: onCreated });
  };
  return {
    architectures,
    create,
    pending: createRepository.isPending,
    error: createRepository.error,
  };
}
