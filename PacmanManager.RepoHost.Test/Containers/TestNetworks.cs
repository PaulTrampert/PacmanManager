using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Logging;

namespace PacmanManager.RepoHost.Test.Containers;

/// <summary>
/// Creates the Docker networks the test fixtures run their containers on.
/// </summary>
/// <remarks>
/// Every network gets a name of its own. Docker refuses a second network by a name that already
/// exists, and network names are global to the daemon, so a name fixed per fixture collides both
/// with another fixture in the same run and with the same fixture running from another worktree on
/// the same machine. Nothing depends on the name: the containers address each other by network
/// alias, which is scoped to the network.
/// </remarks>
public static class TestNetworks
{
    /// <summary>
    /// The prefix every test network's name starts with, so that one left behind is recognisable.
    /// </summary>
    public const string NamePrefix = "pacmanmanager-test-network-";

    /// <summary>
    /// Builds a network with a unique name, removed when it is disposed.
    /// </summary>
    /// <param name="logger">Where Testcontainers logs about the network, or <c>null</c> for its default.</param>
    /// <returns>The network, not yet created.</returns>
    public static INetwork Build(ILogger? logger = null)
    {
        var builder = new NetworkBuilder()
            .WithName($"{NamePrefix}{Guid.NewGuid():N}")
            .WithCleanUp(true);

        return (logger is null ? builder : builder.WithLogger(logger)).Build();
    }
}
