using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end test fixture that runs the PacmanManager.RepoHost application in a Docker container.
/// Uses Testcontainers to build and start the actual Docker image, providing true E2E testing.
/// </summary>
public class EndToEndTestFixture : IAsyncDisposable
{
    private IContainer? _container;
    private HttpClient? _httpClient;

    /// <summary>
    /// Gets the HTTP client for making requests to the containerized API.
    /// </summary>
    public HttpClient HttpClient
    {
        get
        {
            if (_httpClient == null)
                throw new InvalidOperationException("Container has not been started. Call StartAsync() first.");
            return _httpClient;
        }
    }

    /// <summary>
    /// Gets the base URL of the containerized API.
    /// </summary>
    public string BaseUrl => $"http://{_container!.Hostname}:{_container.GetMappedPublicPort(8080)}";

    /// <summary>
    /// Starts the container and initializes the HTTP client.
    /// </summary>
    public async Task StartAsync()
    {
        var logger = new TestOutputLogger(nameof(EndToEndTestFixture));
        try
        {
            // Find the solution directory by searching upwards for the .sln file
            var solutionDirectory = FindSolutionDirectory();
            logger.LogInformation($"Building Docker image from: {solutionDirectory}");

            // Build the Docker image from the Dockerfile
            var image = new ImageFromDockerfileBuilder()
                .WithContextDirectory(solutionDirectory)
                .WithDockerfileDirectory(Path.Combine(solutionDirectory, "PacmanManager.RepoHost"))
                .WithName("pacmanmanager-repohost-test:latest")
                .WithCleanUp(false) // Keep the image for reuse
                .WithLogger(logger)
                .Build();

            // Build the image (this creates it in Docker)
            await image.CreateAsync();

            _container = new ContainerBuilder(image)
                // Map port 8080 from container to a random host port
                .WithPortBinding(8080, true)
                // Wait for the application to be ready
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPort(8080)
                        .ForPath("/api/repository")
                        .ForStatusCode(System.Net.HttpStatusCode.OK)))
                // Clean up after test
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            throw;
        }
    }

    /// <summary>
    /// Stops the container and disposes resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finds the solution directory by searching upwards from the current directory for a .sln file.
    /// </summary>
    /// <returns>The path to the solution directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the solution directory cannot be found.</exception>
    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            // Check if this directory contains a .sln file
            if (directory.GetFiles("*.sln").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not find solution directory. Searched upwards from: " + AppContext.BaseDirectory);
    }
}
