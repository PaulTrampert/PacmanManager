import { useMutation, useQueryClient } from "@tanstack/react-query";
import { client, unwrap, type WriteRepositoryRequest } from "../../api";
import { repositoryKeys } from "./repositoryKeys";

/** What {@link useUpdateRepository}'s `mutate` is given. */
export interface UpdateRepositoryVariables {
  /** The id of the repository to update. */
  id: string;
  /** The repository's new name, architectures and visibility. */
  request: WriteRepositoryRequest;
}

/**
 * Updates a repository with `PUT /v1/Repositories/{id}`. On success it invalidates the repository
 * lists and sets the repository's detail query to the repository the API returns, so its page shows
 * the saved values without refetching them.
 */
export function useUpdateRepository() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: UpdateRepositoryVariables) =>
      unwrap(
        client.PUT("/v1/Repositories/{id}", {
          params: { path: { id } },
          body: request,
        }),
      ),
    onSuccess: (repository, { id }) => {
      void queryClient.invalidateQueries({ queryKey: repositoryKeys.lists() });
      queryClient.setQueryData(repositoryKeys.detail(id), repository);
    },
  });
}
