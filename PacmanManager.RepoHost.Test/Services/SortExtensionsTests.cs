using System.Linq.Expressions;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for the generic <see cref="SortExtensions.ApplySort{TEntity, TSortField}"/>, against a
/// listing of its own so that nothing here depends on a real entity's sort table.
/// </summary>
[TestFixture]
public class SortExtensionsTests
{
    /// <summary>A sort-field enum whose first member defaults to ascending, as a name would.</summary>
    public enum RowSortField
    {
        [DefaultSortDirection(SortDirection.Ascending)]
        Label,

        [DefaultSortDirection(SortDirection.Descending)]
        Rank,

        /// <summary>Declared, but deliberately left out of the key-selector table.</summary>
        [DefaultSortDirection(SortDirection.Ascending)]
        Unmapped,
    }

    /// <summary>The rows being ordered.</summary>
    public record Row(int Id, string Label, int Rank);

    private static readonly Dictionary<RowSortField, Expression<Func<Row, object>>> KeySelectors = new()
    {
        [RowSortField.Label] = r => r.Label,
        [RowSortField.Rank] = r => r.Rank,
    };

    // Ordered differently by label and by rank, so ordering by the wrong key cannot pass.
    private static readonly Row[] Rows =
    [
        new(1, "bravo", 3),
        new(2, "alpha", 1),
        new(3, "charlie", 2),
    ];

    private static IEnumerable<string> Sorted(SortOptions<RowSortField> sort, IEnumerable<Row>? rows = null) =>
        (rows ?? Rows).AsQueryable().ApplySort(sort, KeySelectors, r => r.Id).Select(r => r.Label);

    [TestCase(RowSortField.Label, null, new[] { "alpha", "bravo", "charlie" })]
    [TestCase(RowSortField.Rank, null, new[] { "bravo", "charlie", "alpha" })]
    [TestCase(RowSortField.Label, SortDirection.Descending, new[] { "charlie", "bravo", "alpha" })]
    [TestCase(RowSortField.Rank, SortDirection.Ascending, new[] { "alpha", "charlie", "bravo" })]
    public void ApplySort_OrdersByTheSelectedKey(RowSortField sortBy, SortDirection? direction, string[] expected)
    {
        var sorted = Sorted(new SortOptions<RowSortField> { SortBy = sortBy, Direction = direction });

        Assert.That(sorted, Is.EqualTo(expected));
    }

    [Test]
    public void ApplySort_FallsBackToTheDefaultMembersKey_ForAnUndeclaredSortBy()
    {
        // Model binding can produce a value the enum does not declare. It names no key, so the
        // default member's key is used; the direction is still resolved from the (unknown) field,
        // which falls back to descending.
        var sorted = Sorted(new SortOptions<RowSortField> { SortBy = (RowSortField)999 });

        Assert.That(sorted, Is.EqualTo(new[] { "charlie", "bravo", "alpha" }));
    }

    [Test]
    public void ApplySort_FallsBackToTheDefaultMembersKey_ForAFieldWithNoSelector()
    {
        var sorted = Sorted(new SortOptions<RowSortField> { SortBy = RowSortField.Unmapped });

        Assert.That(sorted, Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));
    }

    [TestCase(SortDirection.Ascending)]
    [TestCase(SortDirection.Descending)]
    public void ApplySort_BreaksTiesByIdAscending_WhicheverWayTheKeyRuns(SortDirection direction)
    {
        Row[] tied = [new(2, "second", 1), new(1, "first", 1), new(3, "third", 1)];

        var sorted = Sorted(
            new SortOptions<RowSortField> { SortBy = RowSortField.Rank, Direction = direction },
            tied);

        Assert.That(sorted, Is.EqualTo(new[] { "first", "second", "third" }));
    }
}
