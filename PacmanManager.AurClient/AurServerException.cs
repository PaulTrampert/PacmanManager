namespace PacmanManager.AurClient;

public class AurServerException(AurErrorResponse response) : Exception(response.Error);
