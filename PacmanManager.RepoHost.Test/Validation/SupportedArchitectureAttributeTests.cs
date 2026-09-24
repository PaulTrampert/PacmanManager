using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Validation;

namespace PacmanManager.RepoHost.Test.Validation;

[TestFixture]
public class SupportedArchitectureAttributeTests
{
    private readonly SupportedArchitectureAttribute _subject = new();
    private readonly ValidationContext _context = new(new object()) { MemberName = "Architecture" };

    [Test]
    public void IsValid_Null_ReturnsSuccess()
    {
        Assert.That(_subject.GetValidationResult(null, _context), Is.Null);
    }

    [Test]
    public void IsValid_SupportedArchitecture_ReturnsSuccess()
    {
        Assert.That(_subject.GetValidationResult(Architectures.X86_64, _context), Is.Null);
    }

    [Test]
    public void IsValid_UnsupportedString_ReturnsError()
    {
        Assert.That(_subject.GetValidationResult("sparc64", _context), Is.Not.Null);
    }

    [Test]
    public void IsValid_Any_WithoutAllowAny_ReturnsError()
    {
        Assert.That(_subject.GetValidationResult(Architectures.Any, _context), Is.Not.Null);
    }

    [Test]
    public void IsValid_Any_WithAllowAny_ReturnsSuccess()
    {
        var subject = new SupportedArchitectureAttribute { AllowAny = true };

        Assert.That(subject.GetValidationResult(Architectures.Any, _context), Is.Null);
    }

    [Test]
    public void IsValid_CollectionOfSupportedArchitectures_ReturnsSuccess()
    {
        Assert.That(_subject.GetValidationResult(new[] { Architectures.X86_64 }, _context), Is.Null);
    }

    [Test]
    public void IsValid_EmptyCollection_ReturnsSuccess()
    {
        // Emptiness is NotEmptyAttribute's concern, not this one's.
        Assert.That(_subject.GetValidationResult(Array.Empty<string>(), _context), Is.Null);
    }

    [Test]
    public void IsValid_CollectionContainingAny_WithoutAllowAny_ReturnsError()
    {
        Assert.That(_subject.GetValidationResult(new[] { Architectures.X86_64, Architectures.Any }, _context),
            Is.Not.Null);
    }

    [Test]
    public void IsValid_CollectionContainingAny_WithAllowAny_ReturnsSuccess()
    {
        var subject = new SupportedArchitectureAttribute { AllowAny = true };

        Assert.That(subject.GetValidationResult(new[] { Architectures.X86_64, Architectures.Any }, _context),
            Is.Null);
    }

    [Test]
    public void IsValid_CollectionContainingAnUnsupportedValue_ReturnsError()
    {
        Assert.That(_subject.GetValidationResult(new[] { Architectures.X86_64, "sparc64" }, _context),
            Is.Not.Null);
    }
}
