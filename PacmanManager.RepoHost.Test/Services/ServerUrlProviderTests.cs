using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// The server URL every repository carries, built from the <c>PublicUrl</c> setting or the request's
/// root, with the setting and the provider registered exactly as <c>Program.cs</c> registers them.
/// </summary>
[TestFixture]
public class ServerUrlProviderTests
{
    private const string Suffix = "pacman/$repo/$arch";

    /// <summary>
    /// Builds the services, with <c>PublicUrl</c> absent when <paramref name="publicUrl"/> is null.
    /// </summary>
    private static ServiceProvider BuildServices(string? publicUrl, params string[] trustedSources)
    {
        var settings = new Dictionary<string, string?>();
        if (publicUrl is not null)
            settings[nameof(PublicUrlConfig.PublicUrl)] = publicUrl;
        for (var i = 0; i < trustedSources.Length; i++)
            settings[$"{ForwardedHeadersConfig.Section}:{nameof(ForwardedHeadersConfig.TrustedSources)}:{i}"] =
                trustedSources[i];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrustedForwardedHeaders(configuration);
        services.AddPublicUrl(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Sends a request through <c>UseForwardedHeaders</c>, as the host's pipeline does, and returns the
    /// server URL built while handling it.
    /// </summary>
    private static async Task<string> SendAsync(IServiceProvider services, Action<HttpContext> arrange)
    {
        var app = new ApplicationBuilder(services);
        app.UseForwardedHeaders();
        string? serverUrl = null;
        app.Run(ctx =>
        {
            serverUrl = ctx.RequestServices.GetRequiredService<IServerUrlProvider>().GetServerUrl();
            return Task.CompletedTask;
        });
        var pipeline = app.Build();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.6");
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("repohost:8080");
        arrange(context);

        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        try
        {
            await pipeline(context);
        }
        finally
        {
            accessor.HttpContext = null;
        }

        return serverUrl!;
    }

    private static Task<string> SendAsync(IServiceProvider services) => SendAsync(services, _ => { });

    [Test]
    public async Task PublicUrlAbsent_UsesTheRequestRoot()
    {
        await using var services = BuildServices(publicUrl: null);

        var serverUrl = await SendAsync(services);

        Assert.That(serverUrl, Is.EqualTo($"http://repohost:8080/{Suffix}"));
    }

    [Test]
    public async Task PublicUrlEmpty_UsesTheRequestRoot()
    {
        await using var services = BuildServices(publicUrl: "");

        var serverUrl = await SendAsync(services);

        Assert.That(serverUrl, Is.EqualTo($"http://repohost:8080/{Suffix}"));
    }

    [TestCase("http://packages.example.com/")]
    [TestCase("https://packages.example.com/")]
    public async Task PublicUrlAnAbsoluteHttpOrHttpsUrl_WinsOverTheRequestRoot(string publicUrl)
    {
        await using var services = BuildServices(publicUrl);

        var serverUrl = await SendAsync(services);

        Assert.That(serverUrl, Is.EqualTo($"{publicUrl}{Suffix}"));
    }

    [Test]
    public void PublicUrlSet_NeedsNoRequest()
    {
        using var services = BuildServices("https://packages.example.com/");

        var serverUrl = services.GetRequiredService<IServerUrlProvider>().GetServerUrl();

        Assert.That(serverUrl, Is.EqualTo($"https://packages.example.com/{Suffix}"));
    }

    [Test]
    public void PublicUrlAbsent_AndNoRequest_Throws()
    {
        using var services = BuildServices(publicUrl: null);

        Assert.Throws<InvalidOperationException>(
            () => services.GetRequiredService<IServerUrlProvider>().GetServerUrl());
    }

    [TestCase("https://example.com/pm")]
    [TestCase("https://example.com/pm/")]
    [TestCase("https://example.com/pm//")]
    public async Task PublicUrl_IsNormalisedToEndInExactlyOneSlash(string publicUrl)
    {
        await using var services = BuildServices(publicUrl);

        var serverUrl = await SendAsync(services);

        Assert.That(serverUrl, Is.EqualTo($"https://example.com/pm/{Suffix}"));
    }

    [Test]
    public async Task RequestRoot_IncludesThePathBase()
    {
        await using var services = BuildServices(publicUrl: null);

        var serverUrl = await SendAsync(services, ctx => ctx.Request.PathBase = "/pm");

        Assert.That(serverUrl, Is.EqualTo($"http://repohost:8080/pm/{Suffix}"));
    }

    [Test]
    public async Task ForwardedHttpsFromATrustedProxy_SetsTheScheme()
    {
        await using var services = BuildServices(publicUrl: null, "10.0.0.6");

        var serverUrl = await SendAsync(services, ctx => ctx.Request.Headers["X-Forwarded-Proto"] = "https");

        Assert.That(serverUrl, Is.EqualTo($"https://repohost:8080/{Suffix}"));
    }

    [Test]
    public async Task ForwardedHttpsFromAnUntrustedCaller_IsIgnored()
    {
        await using var services = BuildServices(publicUrl: null, "10.0.0.5");

        var serverUrl = await SendAsync(services, ctx => ctx.Request.Headers["X-Forwarded-Proto"] = "https");

        Assert.That(serverUrl, Is.EqualTo($"http://repohost:8080/{Suffix}"));
    }

    [Test]
    public async Task ForwardedHost_IsNotBelieved()
    {
        // Only X-Forwarded-For and X-Forwarded-Proto are trusted; the host is always the one the
        // request was addressed to.
        await using var services = BuildServices(publicUrl: null, "10.0.0.6");

        var serverUrl = await SendAsync(services, ctx => ctx.Request.Headers["X-Forwarded-Host"] = "evil.example.com");

        Assert.That(serverUrl, Is.EqualTo($"http://repohost:8080/{Suffix}"));
    }

    [TestCase("/relative/path")]
    [TestCase("packages.example.com")]
    [TestCase("ftp://packages.example.com/")]
    [TestCase(" ")]
    public void PublicUrlAnythingElse_FailsOptionsValidationNamingTheValue(string publicUrl)
    {
        using var services = BuildServices(publicUrl);

        var ex = Assert.Throws<OptionsValidationException>(
            () => _ = services.GetRequiredService<IOptions<PublicUrlConfig>>().Value);
        Assert.That(ex!.Message, Does.Contain($"'{publicUrl}'"));
    }
}
