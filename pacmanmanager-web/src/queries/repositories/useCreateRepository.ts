import { useMutation, useQueryClient } from "@tanstack/react-query";
import { client, unwrap, type WriteRepositoryRequest } from "../../api";
import { repositoryKeys } from "./repositoryKeys";

/**
 * Creates a repository owned by the caller with `POST /v1/Repositories`, resolving to the created
 * repository. On success it invalidates the repository lists, so every list refetches to include
 * it; a failure, such as a `409` for a taken name, rejects with an `ApiError` and changes nothing
 * in the cache.
 */
export function useCreateRepository() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: WriteRepositoryRequest) =>
      unwrap(client.POST("/v1/Repositories", { body })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: repositoryKeys.lists() });
    },
  });
}
