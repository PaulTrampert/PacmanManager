import { useMutation, useQueryClient } from "@tanstack/react-query";
import { client, unwrap } from "../../api";
import { repositoryKeys } from "./repositoryKeys";

/** What {@link useDeleteRepository} may be given. */
export interface UseDeleteRepositoryOptions {
  /**
   * Run once the repository is deleted and the lists invalidated, and awaited before its detail
   * query is removed: typically navigating away from the page that reads that query. The mutation
   * stays pending until it settles.
   */
  onDeleted?: () => Promise<unknown>;
}

/**
 * Deletes a repository, given its id, with `DELETE /v1/Repositories/{id}`. On success it
 * invalidates the repository lists, awaits `onDeleted`, and only then removes the repository's
 * detail query, so a page still reading it never refetches the deleted id.
 *
 * `onDeleted` is an option of the hook rather than a callback passed to `mutate()`: those run after
 * this hook's `onSuccess`, and not at all once the component has unmounted.
 */
export function useDeleteRepository({
  onDeleted,
}: UseDeleteRepositoryOptions = {}) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      unwrap(
        client.DELETE("/v1/Repositories/{id}", { params: { path: { id } } }),
      ),
    onSuccess: async (_data, id) => {
      void queryClient.invalidateQueries({ queryKey: repositoryKeys.lists() });
      await onDeleted?.();
      queryClient.removeQueries({ queryKey: repositoryKeys.detail(id) });
    },
  });
}
