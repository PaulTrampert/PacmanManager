using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Logging;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Containers;

public record KeycloakCredentials(string Username, string Password);

public class KeycloakContainer(INetwork network, string solutionRoot, string? hostname = null) : IAsyncDisposable
{
    private const string DefaultHostname = "keycloak";
    private readonly ILogger _logger = new TestOutputLogger(nameof(KeycloakContainer));

    private readonly IContainer _container = new ContainerBuilder(new DockerImage("quay.io/keycloak/keycloak"))
        .WithNetwork(network)
        .WithNetworkAliases(hostname ?? DefaultHostname)
        .WithBindMount(Path.Combine(solutionRoot, "keycloak/localdev.json"), "/opt/keycloak/data/import/localdev.json")
        .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
        .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
        .WithEnvironment("KC_HOSTNAME", "http://localhost:8080")
        .WithEnvironment("KC_HOSTNAME_BACKCHANNEL_DYNAMIC", "true")
        .WithCommand("start-dev", "--import-realm")
        // A random host port, not a fixed 8080: several fixtures each stand up their own Keycloak,
        // and a fixed binding makes the second one fail with "port is already allocated". Nothing
        // depends on the number -- the API reaches Keycloak over the Docker network by alias, and
        // the test host goes through LocalAuthority, which reads the mapped port back. KC_HOSTNAME
        // still pins the issuer to http://localhost:8080, so tokens are unchanged.
        .WithPortBinding(8080, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r
                .ForPort(8080)
                .ForPath("/realms/localdev/.well-known/openid-configuration")
                .ForStatusCode(HttpStatusCode.OK)))
        .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
        .WithCleanUp(true)
        .Build();

    public string Hostname => hostname ?? DefaultHostname;

    public KeycloakCredentials DefaultCredentials => new("test", "Asdfasdf1");

    /// <summary>
    /// A second realm user, for the cases that need one caller to be somebody other than the owner
    /// of a repository. Anonymous is not enough for those: refusing an unidentified caller and
    /// refusing an identified one who lacks the permission are different answers.
    /// </summary>
    public KeycloakCredentials SecondaryCredentials => new("other", "Asdfasdf1");

    public int Port => _container.GetMappedPublicPort(8080);

    public string Authority => $"http://{Hostname}:8080/realms/localdev";

    public string LocalAuthority => $"http://localhost:{Port}/realms/localdev";

    private HttpClient Client
    {
        get
        {
            if (field is null)
            {
                field = new HttpClient
                {
                    BaseAddress = new Uri($"{LocalAuthority}/"),
                    DefaultRequestHeaders =
                    {
                        Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"pacman-manager-swagger:"))),
                        Accept = { new MediaTypeWithQualityHeaderValue("application/json") },
                        Referrer = new Uri("http://localhost:8082/"),
                    }
                };

                // Inside the null check, because this one is an Add rather than a setter and
                // DefaultRequestHeaders.Add appends. Left outside, every read of this property
                // added another copy, the second request went out with
                // "Origin: http://localhost:8082, http://localhost:8082", and Keycloak refused it
                // with {"error":"Invalid origin"} -- so any fixture that asked for a second token
                // failed in OneTimeSetUp.
                field.DefaultRequestHeaders.Add("Origin", "http://localhost:8082");
            }

            return field;
        }
    }

    public async Task StartAsync()
    {
        await _container.StartAsync();
    }

    public async Task<string> GetBearerTokenAsync(KeycloakCredentials credentials)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", credentials.Username),
            new KeyValuePair<string, string>("password", credentials.Password),
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("scope", "openid pacman-manager profile basic email")
        });
        
        var response = await Client.PostAsync($"protocol/openid-connect/token", content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to get bearer token: {Error}", error);
            throw new Exception($"Failed to get bearer token: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>();
        return json?.AccessToken ?? throw new Exception("Token response did not contain access token.");
    }

    public async ValueTask DisposeAsync()
    {
        if ((_container.State & (TestcontainersStates.Dead | TestcontainersStates.Exited)) == 0)
        {
            await _container.StopAsync();
        }
        await _container.DisposeAsync();
        
        Client.Dispose();
    }

    private record KeycloakTokenResponse
    {
        [JsonPropertyName("access_token")] 
        public required string AccessToken { get; init; }
    }
}
