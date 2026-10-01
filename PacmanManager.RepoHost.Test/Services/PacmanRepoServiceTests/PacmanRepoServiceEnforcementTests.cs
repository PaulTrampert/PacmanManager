using Microsoft.EntityFrameworkCore;
using PacmanManager.RepoHost.Services;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services.PacmanRepoServiceTests;

/// <summary>
/// Guards the structural property that makes <c>PacmanRepoService</c> safe: it owns no database access
/// and no authorization rule, so every visibility outcome falls out of <see cref="IRepositoryService"/>.
/// </summary>
/// <remarks>
/// If one of these fails, resolution has started to state the repository visibility rule a second
/// time. Route the new need through <see cref="IRepositoryService"/> or <see cref="IPackageService"/>
/// rather than relaxing the assertion; see <c>docs/pacman-controller.md</c>, "Why resolution owns no
/// database access".
/// </remarks>
[TestFixture]
public class PacmanRepoServiceEnforcementTests
{
    private string _source = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _source = File.ReadAllText(Path.Combine(
            DirUtils.FindSolutionDirectory(),
            "PacmanManager.RepoHost",
            "Services",
            "PacmanRepoService.cs"));
    }

    [TestCase("PacmanRepositories")]
    [TestCase("PacmanPackages")]
    [TestCase("DbContext")]
    public void TheServiceNamesNoDbSetAndNoContext(string forbidden)
    {
        Assert.That(_source, Does.Not.Contain(forbidden));
    }

    [Test]
    public void TheServiceTakesNoDbContext()
    {
        var parameters = typeof(PacmanRepoService)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType);

        Assert.That(parameters, Has.None.Matches<Type>(t => typeof(DbContext).IsAssignableFrom(t)));
    }

    [Test]
    public void ThereIsNoPacmanRepoAccessPolicy()
    {
        var types = typeof(PacmanRepoService).Assembly.GetTypes().Select(t => t.Name);

        Assert.That(types, Has.None.StartsWith("PacmanRepoAccess"));
    }
}
