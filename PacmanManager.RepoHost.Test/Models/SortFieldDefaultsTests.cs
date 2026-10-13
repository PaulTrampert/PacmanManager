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

    [Test]
    public void Resolve_EchoesAnExplicitFieldAndDirection()
    {
        var sort = new SortOptions<RepositorySortField>
        {
            SortBy = RepositorySortField.Name,
            Direction = SortDirection.Descending
        };

        Assert.That(
            sort.Resolve(),
            Is.EqualTo(new AppliedSort { SortBy = "Name", Direction = SortDirection.Descending }));
    }

    [TestCase(RepositorySortField.Name, "Name", SortDirection.Ascending)]
    [TestCase(RepositorySortField.Created, "Created", SortDirection.Descending)]
    [TestCase(RepositorySortField.Updated, "Updated", SortDirection.Descending)]
    public void Resolve_FillsInTheFieldDefault_WhenNoDirectionWasSupplied(
        RepositorySortField field,
        string expectedSortBy,
        SortDirection expectedDirection)
    {
        var sort = new SortOptions<RepositorySortField> { SortBy = field };

        Assert.That(
            sort.Resolve(),
            Is.EqualTo(new AppliedSort { SortBy = expectedSortBy, Direction = expectedDirection }));
    }

    [Test]
    public void Resolve_ReportsTheDefaultFieldAndItsDirection_WhenNothingWasSupplied()
    {
        Assert.That(
            new SortOptions<RepositorySortField>().Resolve(),
            Is.EqualTo(new AppliedSort { SortBy = "Name", Direction = SortDirection.Ascending }));
    }

    [Test]
    public void Resolve_ReportsTheDefaultField_ForAValueTheEnumDoesNotDeclare()
    {
        // ApplySort orders an undeclared field by the first member, in the fallback direction, so
        // that is what was applied and what the echo must say.
        var sort = new SortOptions<RepositorySortField> { SortBy = (RepositorySortField)999 };

        Assert.That(
            sort.Resolve(),
            Is.EqualTo(new AppliedSort { SortBy = "Name", Direction = SortFieldDefaults<RepositorySortField>.Fallback }));
    }
}
