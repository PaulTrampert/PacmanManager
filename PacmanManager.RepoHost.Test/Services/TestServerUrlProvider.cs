using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// An <see cref="IServerUrlProvider"/> that returns a fixed value, for service tests that run with
/// no request to take a root from.
/// </summary>
internal sealed class TestServerUrlProvider : IServerUrlProvider
{
    /// <summary>
    /// The value every call returns.
    /// </summary>
    public const string ServerUrl = "https://packages.example.com/pacman/$repo/$arch";

    /// <inheritdoc />
    public string GetServerUrl() => ServerUrl;
}
