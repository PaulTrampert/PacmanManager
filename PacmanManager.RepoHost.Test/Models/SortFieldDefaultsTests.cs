using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test.Models;

/// <summary>
/// Tests for the per-field default sort directions, which are the one place both the ordering and
/// the API documentation of <c>direction</c> read.
/// </summary>
[TestFixture]
public class SortFieldDefaultsTests
{
    [TestCase(RepositorySortField.Name, SortDirection.Ascending)]
    [TestCase(RepositorySortField.Created, SortDirection.Descending)]
    [TestCase(RepositorySortField.Updated, SortDirection.Descending)]
    public void For_ReturnsTheDirectionDeclaredOnTheField(
        RepositorySortField field,
        SortDirection expected)
    {
        Assert.That(SortFieldDefaults<RepositorySortField>.For(field), Is.EqualTo(expected));
    }

    [Test]
    public void For_FallsBackToDescending_ForAValueTheEnumDoesNotDeclare()
    {
        // Model binding will happily produce an out-of-range enum value, so the lookup must answer
        // for one rather than throwing.
        Assert.That(
            SortFieldDefaults<RepositorySortField>.For((RepositorySortField)999),
            Is.EqualTo(SortDirection.Descending));
    }

    [Test]
    public void Describe_NamesEveryFieldGroupedByDirection()
    {
        Assert.That(
            SortFieldDefaults<RepositorySortField>.Describe(),
            Is.EqualTo("When omitted, defaults to Ascending for Name; Descending for Created, Updated."));
    }

    [Test]
    public void ResolveDirection_PrefersTheSuppliedDirection()
    {
        // An explicit direction always wins, so ?sortBy=Name&direction=Descending is still Z→A.
        var sort = new SortOptions<RepositorySortField>
        {
            SortBy = RepositorySortField.Name,
            Direction = SortDirection.Descending
        };

        Assert.That(sort.ResolveDirection(), Is.EqualTo(SortDirection.Descending));
    }

    [Test]
    public void ResolveDirection_FallsBackToTheFieldDefault_WhenNoneWasSupplied()
    {
        var sort = new SortOptions<RepositorySortField> { SortBy = RepositorySortField.Name };

        Assert.Multiple(() =>
        {
            Assert.That(sort.Direction, Is.Null);
            Assert.That(sort.ResolveDirection(), Is.EqualTo(SortDirection.Ascending));
        });
    }
}
