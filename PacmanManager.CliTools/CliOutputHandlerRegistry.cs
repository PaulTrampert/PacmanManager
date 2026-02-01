using Microsoft.Extensions.DependencyInjection;

namespace PacmanManager.CliTools;

/// <summary>
/// Implementation of <see cref="ICliOutputHandlerRegistry"/> that retrieves global output handlers from the service provider.
/// </summary>
/// <param name="provider"></param>
public class CliOutputHandlerRegistry(IServiceProvider provider) : ICliOutputHandlerRegistry
{
    /// <inheritdoc />
    public IEnumerable<ICliOutputHandler> GetGlobalOutputHandlers()
    {
        return provider.GetServices<ICliOutputHandler>();
    }
}