using System.Globalization;
using System.Reflection;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test.Models;

/// <summary>
/// Tests for <see cref="UserFilter"/>, whose shape is part of the anonymous listing's privacy.
/// </summary>
[TestFixture]
public class UserFilterTests
{
    [Test]
    public void DisplayNameContains_IsLoweredWithTheInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // Under tr-TR, culture-sensitive lowering turns I into a dotless ı, which would never
            // match a NormalizedDisplayName written with the invariant culture.
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            var filter = new UserFilter { DisplayNameContains = "IAN" };

            Assert.That(filter.DisplayNameContains, Is.EqualTo("ian"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public void DisplayNameContains_WhenNull_StaysNull()
    {
        Assert.That(new UserFilter { DisplayNameContains = null }.DisplayNameContains, Is.Null);
    }

    [Test]
    public void NoMemberReachesEmail()
    {
        // An emailContains would make the anonymous listing an oracle for whether a person has an
        // account here. The filter's members, and the columns their query attributes name, must not
        // mention it.
        var mentions = typeof(UserFilter)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(p => new[] { p.Name }.Concat(p.GetCustomAttributesData()
                .SelectMany(a => a.ConstructorArguments)
                .Select(arg => arg.Value?.ToString() ?? string.Empty)))
            .Where(text => text.Contains(nameof(User.Email), StringComparison.OrdinalIgnoreCase));

        Assert.That(mentions, Is.Empty);
    }
}
