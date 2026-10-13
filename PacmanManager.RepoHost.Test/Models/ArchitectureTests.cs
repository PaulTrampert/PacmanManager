using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test.Models;

/// <summary>
/// The supported architectures as the API describes them.
/// </summary>
[TestFixture]
public class ArchitectureTests
{
    private static IEnumerable<string> SupportedArchitectures =>
        PacmanRepositoryValidationConstants.SupportedArchitectures;

    [TestCaseSource(nameof(SupportedArchitectures))]
    public void EverySupportedArchitecture_HasAFriendlyName(string architecture)
    {
        PacmanRepositoryValidationConstants.ArchitectureFriendlyNames.TryGetValue(architecture, out var name);

        Assert.That(string.IsNullOrWhiteSpace(name), Is.False,
            $"{architecture} is supported but has no friendly name");
    }

    [Test]
    public void FromValue_DisplaysTheFriendlyNameThenTheValue()
    {
        var architecture = Architecture.FromValue(Architectures.X86_64);

        Assert.Multiple(() =>
        {
            Assert.That(architecture.Value, Is.EqualTo("x86_64"));
            Assert.That(architecture.DisplayValue, Is.EqualTo("64-bit Intel/AMD (x86_64)"));
        });
    }

    [Test]
    public void Supported_ListsExactlyTheSupportedArchitectures_InOrder()
    {
        Assert.That(Architecture.Supported.Select(a => a.Value), Is.EqualTo(SupportedArchitectures));
    }
}
