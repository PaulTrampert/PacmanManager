using System.Net;
using System.Text;

namespace PacmanManager.AurClient.Test;

[TestFixture]
public class AurClientUserAgentTests
{
    private const string SearchResponse = """{"version":5,"type":"search","resultcount":0,"results":[]}""";

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SearchResponse, Encoding.UTF8, "application/json")
            });
        }
    }

    [Test]
    public async Task Search_WithoutClientUserAgent_SendsDefaultUserAgent()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://aur.example") };

        await new AurClient(httpClient).Search("yay", AurSearchBy.Name);

        var userAgent = handler.Request!.Headers.UserAgent.Single();
        Assert.That(userAgent.Product?.Name, Is.EqualTo("PacmanManager.AurClient"));
        Assert.That(userAgent.Product?.Version, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Search_WithClientUserAgent_KeepsClientUserAgent()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://aur.example") };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Custom/1.0");

        await new AurClient(httpClient).Search("yay", AurSearchBy.Name);

        Assert.That(handler.Request!.Headers.UserAgent.ToString(), Is.EqualTo("Custom/1.0"));
    }
}
