using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace PacmanManager.TestUtils;



public class UntilExitWaitStrategy : IWaitUntil
{
    public Task<bool> UntilAsync(IContainer container)
    {
        return Task.FromResult((container.State & (TestcontainersStates.Exited | TestcontainersStates.Dead)) > 0);
    }
}