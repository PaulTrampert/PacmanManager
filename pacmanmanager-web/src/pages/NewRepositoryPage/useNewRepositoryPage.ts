import { useSuspenseQuery } from "@tanstack/react-query";
import type { Architecture } from "../../api";
import { architecturesQuery } from "../../queries/architectures";

/** What {@link useNewRepositoryPage} gives the page to render. */
export interface NewRepositoryPageState {
  /** Every architecture a repository may support, for the form. */
  architectures: readonly Architecture[];
}

/**
 * The new repository page's behaviour: it reads the architectures the route's loader ensured, so
 * it never suspends there.
 */
export function useNewRepositoryPage(): NewRepositoryPageState {
  const { data: architectures } = useSuspenseQuery(architecturesQuery());
  return { architectures };
}
