using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PacmanManager.RepoHost.Config;

namespace PacmanManager.RepoHost.Test.Config;

/// <summary>
/// Runs requests through <c>UseForwardedHeaders</c>, registered exactly as <c>Program.cs</c> registers
/// it, and checks which callers are allowed to set the client address.
/// </summary>
[TestFixture]
public class ForwardedHeadersPipelineTests
{
    private const string ForwardedClient = "203.0.113.7";

    private static IServiceProvider BuildServices(params string[] trustedSources)
    {
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < trustedSources.Length; i++)
            settings[$"{ForwardedHeadersConfig.Section}:{nameof(ForwardedHeadersConfig.TrustedSources)}:{i}"] =
                trustedSources[i];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrustedForwardedHeaders(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Sends a request from <paramref name="remoteAddress"/> carrying <c>X-Forwarded-For</c>, and
    /// returns the <see cref="ConnectionInfo.RemoteIpAddress"/> the rest of the pipeline sees.
    /// </summary>
    private static async Task<IPAddress?> SendAsync(IServiceProvider services, string remoteAddress)
    {
        var app = new ApplicationBuilder(services);
        app.UseForwardedHeaders();
        IPAddress? seen = null;
        app.Run(ctx =>
        {
            seen = ctx.Connection.RemoteIpAddress;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Headers["X-Forwarded-For"] = ForwardedClient;
        await pipeline(context);
        return seen;
    }

    [Test]
    public async Task TrustedAddress_ForwardedForIsReportedAsRemoteIpAddress()
    {
        var services = BuildServices("10.0.0.5");

        var seen = await SendAsync(services, "10.0.0.5");

        Assert.That(seen, Is.EqualTo(IPAddress.Parse(ForwardedClient)));
    }

    [Test]
    public async Task AddressInTrustedSubnet_ForwardedForIsReportedAsRemoteIpAddress()
    {
        var services = BuildServices("172.16.0.0/12");

        var seen = await SendAsync(services, "172.18.0.4");

        Assert.That(seen, Is.EqualTo(IPAddress.Parse(ForwardedClient)));
    }

    [Test]
    public async Task IPv4MappedTrustedAddress_ForwardedForIsReportedAsRemoteIpAddress()
    {
        var services = BuildServices("172.16.0.0/12");

        var seen = await SendAsync(services, "::ffff:172.18.0.4");

        Assert.That(seen, Is.EqualTo(IPAddress.Parse(ForwardedClient)));
    }

    [Test]
    public async Task UntrustedAddress_ForwardedForIsIgnored()
    {
        var services = BuildServices("10.0.0.5");

        var seen = await SendAsync(services, "10.0.0.6");

        Assert.That(seen, Is.EqualTo(IPAddress.Parse("10.0.0.6")));
    }

    [Test]
    public async Task EmptySection_ForwardedForFromNonLoopbackIsIgnored()
    {
        var services = BuildServices();

        var seen = await SendAsync(services, "10.0.0.6");

        Assert.That(seen, Is.EqualTo(IPAddress.Parse("10.0.0.6")));
    }

    [Test]
    public async Task EmptySection_ForwardedForFromLoopbackIsBelieved()
    {
        var services = BuildServices();

        var seen = await SendAsync(services, "127.0.0.1");

        Assert.That(seen, Is.EqualTo(IPAddress.Parse(ForwardedClient)));
    }

    [Test]
    public void UnparseableEntry_FailsOptionsValidationNamingTheEntry()
    {
        var services = BuildServices("10.0.0.5", "not-an-address");

        var ex = Assert.Throws<OptionsValidationException>(
            () => _ = services.GetRequiredService<IOptions<ForwardedHeadersConfig>>().Value);
        Assert.That(ex!.Message, Does.Contain("'not-an-address'"));
    }
}
