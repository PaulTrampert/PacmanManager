using System.Net;
using System.Net.Http.Headers;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for the Architectures API.
/// These tests run the application in a Docker container and make real HTTP requests.
/// </summary>
/// <remarks>
/// The response is checked as raw JSON, not a deserialised model, so that a property the server
/// started sending, or a change of shape such as wrapping the array in a page, fails the test.
/// </remarks>
[TestFixture]
public class ArchitecturesControllerTests
{
    private EndToEndTestFixture _fixture = null!;
    private HttpClient _anonymousClient = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();

        _anonymousClient = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _anonymousClient.Dispose();
        await _fixture.DisposeAsync();
    }

    [Test]
    public async Task Get_Anonymously_ReturnsThePlainArrayOfSupportedArchitectures()
    {
        // Act
        var response = await _anonymousClient.GetAsync("/api/v1/architectures");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Is.EqualTo("""[{"value":"x86_64","displayValue":"64-bit Intel/AMD (x86_64)"}]"""));
        });
    }

    [Test]
    public async Task Get_Authenticated_ReturnsTheSameArchitectures()
    {
        // Arrange
        var client = _fixture.HttpClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));

        // Act
        var response = await client.GetAsync("/api/v1/architectures");
        var body = await response.Content.ReadAsStringAsync();
        var anonymousBody = await _anonymousClient.GetStringAsync("/api/v1/architectures");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Is.EqualTo(anonymousBody));
        });
    }
}
