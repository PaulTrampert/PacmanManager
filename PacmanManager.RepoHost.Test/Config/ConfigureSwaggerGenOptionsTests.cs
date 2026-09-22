using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Config;

namespace PacmanManager.RepoHost.Test.Config;

/// <summary>
/// Tests the scopes the Swagger security requirement lists.
/// </summary>
[TestFixture]
public class ConfigureSwaggerGenOptionsTests
{
    [Test]
    public void RequestedScopes_IncludesTheAudienceAndEveryScopeValue()
    {
        Assert.That(ConfigureSwaggerGenOptions.RequestedScopes,
            Is.SupersetOf(ScopeValues.All.Append(ScopeValues.Audience).Append("openid")));
        Assert.That(ConfigureSwaggerGenOptions.RequestedScopes, Is.Unique);
    }
}
