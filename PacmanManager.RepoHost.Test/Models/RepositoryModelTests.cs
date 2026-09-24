using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test.Models;

/// <summary>
/// Validation of the architectures a repository supports, on each wire model that names one.
/// </summary>
[TestFixture]
public class RepositoryModelTests
{
    [Test]
    public void Request_DefaultsToSupportingEveryAllowedArchitecture()
    {
        var request = new WriteRepositoryRequest { Name = "custom" };

        Assert.Multiple(() =>
        {
            Assert.That(request.SupportedArchitectures,
                Is.EqualTo(PacmanRepositoryValidationConstants.SupportedArchitectures));
            Assert.That(Validate(request), Is.Empty);
        });
    }

    [Test]
    public void Request_SupportingEveryAllowedArchitecture_IsValid()
    {
        var request = new WriteRepositoryRequest
        {
            Name = "custom",
            SupportedArchitectures = PacmanRepositoryValidationConstants.SupportedArchitectures,
        };

        Assert.That(Validate(request), Is.Empty);
    }

    [Test]
    public void Request_SupportingAny_IsInvalid()
    {
        // any describes a package, never a repository.
        var request = new WriteRepositoryRequest { Name = "custom", SupportedArchitectures = [Architectures.Any] };

        AssertInvalidSupportedArchitectures(request);
    }

    [Test]
    public void Request_SupportingAnyAlongsideARealArchitecture_IsInvalid()
    {
        var request = new WriteRepositoryRequest
        {
            Name = "custom",
            SupportedArchitectures = [Architectures.X86_64, Architectures.Any],
        };

        AssertInvalidSupportedArchitectures(request);
    }

    [Test]
    public void Request_SupportingNothing_IsInvalid()
    {
        // A repository that supports nothing can serve nothing.
        var request = new WriteRepositoryRequest { Name = "custom", SupportedArchitectures = [] };

        AssertInvalidSupportedArchitectures(request);
    }

    [TestCase("sparc64")]
    [TestCase("X86_64")]
    [TestCase("")]
    [TestCase("x86_64 ")]
    public void Request_SupportingAnUnknownString_IsInvalid(string architecture)
    {
        var request = new WriteRepositoryRequest { Name = "custom", SupportedArchitectures = [architecture] };

        AssertInvalidSupportedArchitectures(request);
    }

    [Test]
    public void Request_SupportingANullElement_IsInvalid()
    {
        var request = new WriteRepositoryRequest { Name = "custom", SupportedArchitectures = [null!] };

        AssertInvalidSupportedArchitectures(request);
    }

    [Test]
    public void Request_WithNullSupportedArchitectures_IsInvalid()
    {
        var request = new WriteRepositoryRequest { Name = "custom", SupportedArchitectures = null! };

        AssertInvalidSupportedArchitectures(request);
    }

    [Test]
    public void AllowedSet_DoesNotContainAny()
    {
        Assert.That(PacmanRepositoryValidationConstants.SupportedArchitectures, Does.Not.Contain(Architectures.Any));
    }

    [Test]
    public void DefaultArchitecture_IsEveryAllowedArchitecture()
    {
        Assert.That(PacmanRepositoryValidationConstants.DefaultArchitecture,
            Is.EqualTo(PacmanRepositoryValidationConstants.SupportedArchitectures));
    }

    [Test]
    public void Filter_WithoutAnArchitecture_IsValid()
    {
        Assert.That(Validate(new RepositoryFilter()), Is.Empty);
    }

    [Test]
    public void Filter_WithASupportedArchitecture_IsValid()
    {
        Assert.That(Validate(new RepositoryFilter { Architecture = Architectures.X86_64 }), Is.Empty);
    }

    [TestCase(Architectures.Any)]
    [TestCase("sparc64")]
    public void Filter_WithAnArchitectureNoRepositoryMaySupport_IsInvalid(string architecture)
    {
        var errors = Validate(new RepositoryFilter { Architecture = architecture });

        Assert.That(errors.SelectMany(e => e.MemberNames), Is.EqualTo(new[] { nameof(RepositoryFilter.Architecture) }));
    }

    private static void AssertInvalidSupportedArchitectures(WriteRepositoryRequest request)
    {
        var errors = Validate(request);

        Assert.That(errors.SelectMany(e => e.MemberNames),
            Is.EqualTo(new[] { nameof(WriteRepositoryRequest.SupportedArchitectures) }));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
