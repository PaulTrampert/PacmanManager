using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PacmanManager.Entities;
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
    /// The package upload ceiling the containerized API is configured with, in bytes.
    /// </summary>
    public const long MaxUploadBytes = 1024 * 1024;

    /// <summary>
    /// A context over the same database the containerized API is using, reachable from the test
    /// host.
    /// </summary>
    /// <remarks>
    /// This is for arranging rows directly, where publishing them through the API would test the
    /// arrangement rather than the endpoint under test, and not for asserting: an assertion belongs
    /// against the API's own responses. The caller owns the returned context.
    /// </remarks>
    /// <returns>A context connected to the test database.</returns>
    /// <exception cref="InvalidOperationException">The containers have not been started.</exception>
    public PacmanManagerDbContext CreateDbContext()
    {
        if (_dbContainer == null)
            throw new InvalidOperationException("Container has not been started. Call StartAsync() first.");

        return new PacmanManagerDbContext(new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseNpgsql(_dbContainer.LocalConnectionString)
            .Options);
    }

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
            var solutionDirectory = DirUtils.FindSolutionDirectory();
            logger.LogInformation($"Building Docker image from: {solutionDirectory}");

            // Named per fixture rather than once for the suite: more than one fixture now runs a
            // stack of its own, and Docker rejects a second network by an existing name. The
            // containers address each other by network alias, which is scoped to the network, so
            // the name itself is never something a test depends on.
            _testNetwork = new NetworkBuilder()
                .WithName($"pacmanmanager-test-network-{Guid.NewGuid():N}")
                .WithCleanUp(true)
                .WithLogger(logger)
                .Build();

            // Build the Docker image from the Dockerfile.
            //
            // The Dockerfile directory is the solution root rather than the project directory, and
            // the project directory rides along in the Dockerfile path instead. That is what makes
            // the root .dockerignore apply: Testcontainers looks for the ignore file next to the
            // *Dockerfile directory* -- "<dockerfile>.dockerignore", falling back to
            // ".dockerignore" there -- and never reads the context root's. Point the Dockerfile
            // directory at a project and the root ignore file is silently skipped, so the host's
            // bin/ and obj/ are tarred into the context; obj/ records absolute host paths (the
            // sources ANTLR generates for LibAlpmSharp among them) and `dotnet build` inside the
            // image then fails with CS2001. That only bites once a local build has populated obj/,
            // which is always true in CI.
            var apiImage = new ImageFromDockerfileBuilder()
                .WithContextDirectory(solutionDirectory)
                .WithDockerfileDirectory(solutionDirectory)
                .WithDockerfile("PacmanManager.RepoHost/Dockerfile")
                .WithName("pacmanmanager-repohost-test:latest")
                .WithCleanUp(false) // Keep the image for reuse
                .WithLogger(logger)
                .Build();

            var migrationImage = new ImageFromDockerfileBuilder()
                .WithContextDirectory(solutionDirectory)
                .WithDockerfileDirectory(solutionDirectory)
                .WithDockerfile("PacmanManager.Migrations/Dockerfile")
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
                // Well above the fixture package and low enough that a test can exceed it, which is
                // the point of the limit being configuration rather than a constant.
                .WithEnvironment("PackagePublishing__MaxUploadBytes", MaxUploadBytes.ToString())
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
    /// Runs a command inside the API container and returns what it wrote to standard output.
    /// </summary>
    /// <param name="command">The command and its arguments.</param>
    /// <returns>The command's standard output.</returns>
    /// <exception cref="InvalidOperationException">The container is not running, or the command failed.</exception>
    /// <remarks>
    /// This is how a test inspects what the pacman tools actually wrote into a repository's
    /// <c>.db.tar.gz</c>, which no HTTP route exposes and which is exactly the artefact a pacman
    /// client would read.
    /// </remarks>
    public async Task<string> ExecInApiContainerAsync(params string[] command)
    {
        if (_apiContainer is null)
        {
            throw new InvalidOperationException("Container has not been started. Call StartAsync() first.");
        }

        var result = await _apiContainer.ExecAsync(command);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{string.Join(' ', command)}' exited with {result.ExitCode}: {result.Stderr}");
        }

        return result.Stdout;
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

        // Kept with the rest: a container still attached to the network keeps the network alive, and
        // Keycloak binds a fixed host port, so leaving it running blocks the next fixture twice over.
        if (AuthContainer != null)
        {
            await AuthContainer.DisposeAsync();
            AuthContainer = null;
        }

        if (_testNetwork != null)
        {
            await _testNetwork.DisposeAsync();
            _testNetwork = null;
        }

        GC.SuppressFinalize(this);
    }
}
