using Microsoft.Extensions.Configuration;
using PacmanManager.RepoHost.Config;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Config;

/// <summary>
/// Tests that <see cref="AccessTokenConfig.LastUsedAtResolution"/> defaults to one hour and is
/// discoverable in <c>appsettings.json</c>.
/// </summary>
[TestFixture]
public class AccessTokenConfigTests
{
    [Test]
    public void LastUsedAtResolution_DefaultsToOneHour()
    {
        Assert.That(new AccessTokenConfig().LastUsedAtResolution, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void AppSettings_CarriesLastUsedAtResolution_AtOneHour()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(DirUtils.FindSolutionDirectory(), "PacmanManager.RepoHost", "appsettings.json"))
            .Build();
        var section = configuration.GetSection(AccessTokenConfig.Section);

        Assert.Multiple(() =>
        {
            Assert.That(section[nameof(AccessTokenConfig.LastUsedAtResolution)], Is.Not.Null,
                "appsettings.json should carry the key so that it is discoverable.");
            Assert.That(section.Get<AccessTokenConfig>()?.LastUsedAtResolution, Is.EqualTo(TimeSpan.FromHours(1)));
        });
    }

    [Test]
    public void LastUsedAtResolution_BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AccessTokenConfig.Section}:{nameof(AccessTokenConfig.LastUsedAtResolution)}"] = "00:05:00"
            })
            .Build();

        var config = configuration.GetSection(AccessTokenConfig.Section).Get<AccessTokenConfig>();

        Assert.That(config?.LastUsedAtResolution, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }
}
