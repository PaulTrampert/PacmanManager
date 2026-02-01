using Microsoft.Extensions.DependencyInjection;

namespace PacmanManager.CliTools;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCliToolRunner(this IServiceCollection serviceCollection)
    {
        return serviceCollection.AddScoped<ICliToolRunner, CliToolRunner>()
            .AddScoped<ICliOutputHandlerRegistry, CliOutputHandlerRegistry>();
    }

    public static IServiceCollection AddGlobalCliOutputHandler<THandler>(this IServiceCollection serviceCollection)
        where THandler : class, ICliOutputHandler
    {
        return serviceCollection.AddScoped<ICliOutputHandler, THandler>();
    }

    public static IServiceCollection AddCliOutputLogger(this IServiceCollection serviceCollection)
    {
        return serviceCollection.AddGlobalCliOutputHandler<LoggingCliOutputHandler>();
    }
}
