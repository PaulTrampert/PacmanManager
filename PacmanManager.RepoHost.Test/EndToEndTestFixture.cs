using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Logging;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end test fixture that runs the PacmanManager.RepoHost application in a Docker container.
/// Uses Testcontainers to build and start the actual Docker image, providing true E2E testing.
/// </summary>
public class EndToEndTestFixture : IAsyncDisposable
{
    private INetwork? _testNetwork;
    private DatabaseContainer? _dbContainer;
    public KeycloakContainer? AuthContainer { get; private set; }
    private IContainer? _apiContainer;
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
    public string BaseUrl => $"http://{_apiContainer!.Hostname}:{_apiContainer.GetMappedPublicPort(8080)}";

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

            _testNetwork = new NetworkBuilder()
                .WithName("pacmanmanager-test-network")
                .WithCleanUp(true)
                .WithLogger(logger)
                .Build();

            // Build the Docker image from the Dockerfile
            var apiImage = new ImageFromDockerfileBuilder()
                .WithContextDirectory(solutionDirectory)
                .WithDockerfileDirectory(Path.Combine(solutionDirectory, "PacmanManager.RepoHost"))
                .WithName("pacmanmanager-repohost-test:latest")
                .WithCleanUp(false) // Keep the image for reuse
                .WithLogger(logger)
                .Build();

            var migrationImage = new ImageFromDockerfileBuilder()
                .WithContextDirectory(solutionDirectory)
                .WithDockerfileDirectory(Path.Combine(solutionDirectory, "PacmanManager.Migrations"))
                .WithName("pacmanmanager-migrations-test:latest")
                .WithCleanUp(false) // Keep the image for reuse
                .WithLogger(logger)
                .Build();

            // Build the image (this creates it in Docker)
            await Task.WhenAll(apiImage.CreateAsync(), migrationImage.CreateAsync());

            _dbContainer = new DatabaseContainer(_testNetwork);
            AuthContainer = new KeycloakContainer(_testNetwork, solutionDirectory);
            
            await Task.WhenAll(_dbContainer.StartAsync(migrationImage), AuthContainer.StartAsync());
                
            _apiContainer = new ContainerBuilder(apiImage)
                .WithNetwork(_testNetwork)
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                .WithEnvironment("ConnectionStrings__pacmanmanager", $"Server={_dbContainer.Hostname};User Id=pacmanmanager;Password=password;")
                .WithEnvironment("Auth__Authority", AuthContainer.Authority)
                // Map port 8080 from container to a random host port
                .WithPortBinding(8080, true)
                // Wait for the application to be ready
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPort(8080)
                        .ForPath("/api/v1/healthcheck")
                        .ForStatusCode(System.Net.HttpStatusCode.OK)))
                // Clean up after test
                .WithCleanUp(true)
                .WithLogger(logger)
                .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
                .Build();

            await _apiContainer.StartAsync();

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

        if (_apiContainer != null)
        {
            await _apiContainer.StopAsync();
            await _apiContainer.DisposeAsync();
            _apiContainer = null;
        }

        if (_dbContainer != null)
        {
            await _dbContainer.DisposeAsync();
            _dbContainer = null;
        }

        if (_testNetwork != null)
        {
            await _testNetwork.DisposeAsync();
            _testNetwork = null;
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
