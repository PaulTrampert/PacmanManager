using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using PacmanManager.RepoHost.Config;
using IPNetwork = System.Net.IPNetwork;

namespace PacmanManager.RepoHost.Test.Config;

/// <summary>
/// Tests that each <see cref="ForwardedHeadersConfig.TrustedSources"/> entry lands in the right
/// <see cref="ForwardedHeadersOptions"/> list, that the loopback defaults survive, and that an
/// unparseable entry fails validation naming it.
/// </summary>
[TestFixture]
public class ForwardedHeadersConfigTests
{
    private static ForwardedHeadersOptions Configure(params string[] trustedSources)
    {
        var options = new ForwardedHeadersOptions();
        var configurer = new ConfigureForwardedHeadersOptions(
            Options.Create(new ForwardedHeadersConfig { TrustedSources = [..trustedSources] }));
        configurer.Configure(options);
        return options;
    }

    [Test]
    public void Configure_SingleIpv4Address_IsAddedToKnownProxies()
    {
        var options = Configure("10.0.0.5");

        Assert.Multiple(() =>
        {
            Assert.That(options.KnownProxies, Does.Contain(IPAddress.Parse("10.0.0.5")));
            Assert.That(options.KnownIPNetworks, Has.None.EqualTo(IPNetwork.Parse("10.0.0.5/32")));
        });
    }

    [Test]
    public void Configure_SingleIpv6Address_IsAddedToKnownProxies()
    {
        var options = Configure("fd00::5");

        Assert.That(options.KnownProxies, Does.Contain(IPAddress.Parse("fd00::5")));
    }

    [Test]
    public void Configure_Ipv4Cidr_IsAddedToKnownIPNetworks()
    {
        var options = Configure("172.16.0.0/12");

        Assert.Multiple(() =>
        {
            Assert.That(options.KnownIPNetworks, Does.Contain(IPNetwork.Parse("172.16.0.0/12")));
            Assert.That(options.KnownProxies, Has.None.EqualTo(IPAddress.Parse("172.16.0.0")));
        });
    }

    [Test]
    public void Configure_Ipv6Cidr_IsAddedToKnownIPNetworks()
    {
        var options = Configure("fd00::/8");

        Assert.That(options.KnownIPNetworks, Does.Contain(IPNetwork.Parse("fd00::/8")));
    }

    [Test]
    public void Configure_ForwardsForAndProto()
    {
        var options = Configure();

        Assert.That(options.ForwardedHeaders,
            Is.EqualTo(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto));
    }

    [Test]
    public void Configure_WithNoSources_KeepsTheLoopbackDefaults()
    {
        var defaults = new ForwardedHeadersOptions();

        var options = Configure();

        Assert.Multiple(() =>
        {
            Assert.That(options.KnownProxies, Is.Not.Empty);
            Assert.That(options.KnownIPNetworks, Is.Not.Empty);
            Assert.That(options.KnownProxies, Is.EquivalentTo(defaults.KnownProxies));
            Assert.That(options.KnownIPNetworks, Is.EquivalentTo(defaults.KnownIPNetworks));
        });
    }

    [Test]
    public void Configure_WithSources_AddsToTheLoopbackDefaultsRatherThanReplacingThem()
    {
        var defaults = new ForwardedHeadersOptions();

        var options = Configure("10.0.0.5", "172.16.0.0/12");

        Assert.Multiple(() =>
        {
            Assert.That(options.KnownProxies, Is.SupersetOf(defaults.KnownProxies));
            Assert.That(options.KnownIPNetworks, Is.SupersetOf(defaults.KnownIPNetworks));
        });
    }

    [TestCase("10.0.0.5")]
    [TestCase("fd00::5")]
    [TestCase("172.16.0.0/12")]
    [TestCase("fd00::/8")]
    public void Validate_ParseableEntry_Succeeds(string entry)
    {
        var result = new ValidateForwardedHeadersConfig()
            .Validate(null, new ForwardedHeadersConfig { TrustedSources = [entry] });

        Assert.That(result.Succeeded, Is.True);
    }

    [TestCase("not-an-address")]
    [TestCase("10.0.0.300")]
    [TestCase("172.16.0.0/40")]
    [TestCase("proxy.example/24")]
    [TestCase("")]
    public void Validate_UnparseableEntry_FailsNamingTheEntry(string entry)
    {
        var result = new ValidateForwardedHeadersConfig()
            .Validate(null, new ForwardedHeadersConfig { TrustedSources = ["10.0.0.5", entry] });

        Assert.Multiple(() =>
        {
            Assert.That(result.Failed, Is.True);
            Assert.That(result.Failures, Has.Exactly(1).Contains($"'{entry}'"));
        });
    }

    [Test]
    public void Validate_EmptyList_Succeeds()
    {
        var result = new ValidateForwardedHeadersConfig().Validate(null, new ForwardedHeadersConfig());

        Assert.That(result.Succeeded, Is.True);
    }
}
