using System.ComponentModel.DataAnnotations;
using PacmanManager.RepoHost.Validation;

namespace PacmanManager.RepoHost.Test.Validation;

[TestFixture]
public class NotEmptyAttributeTests
{
    private readonly NotEmptyAttribute _subject = new();
    private readonly ValidationContext _context = new(new object()) { MemberName = "Items" };

    [Test]
    public void IsValid_Null_ReturnsSuccess()
    {
        Assert.That(_subject.GetValidationResult(null, _context), Is.Null);
    }

    [Test]
    public void IsValid_EmptyList_ReturnsError()
    {
        Assert.That(_subject.GetValidationResult(new List<string>(), _context), Is.Not.Null);
    }

    [Test]
    public void IsValid_EmptyDictionary_ReturnsError()
    {
        Assert.That(_subject.GetValidationResult(new Dictionary<string, string>(), _context), Is.Not.Null);
    }

    [Test]
    public void IsValid_NonEmptyList_ReturnsSuccess()
    {
        Assert.That(_subject.GetValidationResult(new List<string> { "a" }, _context), Is.Null);
    }

    [Test]
    public void IsValid_NonEmptyDictionary_ReturnsSuccess()
    {
        Assert.That(_subject.GetValidationResult(new Dictionary<string, string> { ["a"] = "b" }, _context), Is.Null);
    }

    [Test]
    public void IsValid_NonCollectionValue_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _subject.GetValidationResult(42, _context));
    }
}
