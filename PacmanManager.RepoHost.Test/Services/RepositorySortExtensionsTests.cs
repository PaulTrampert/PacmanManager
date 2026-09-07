using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for the ordering <see cref="RepositorySortExtensions.ApplySort"/> applies, and in
/// particular for the direction it picks when the caller supplied none.
/// </summary>
/// <remarks>
/// The fixture data orders the three repositories differently by every sortable property, so a
/// case that ordered by the wrong property — or in the wrong direction — cannot accidentally
/// produce the expected sequence.
/// </remarks>
[TestFixture]
public class RepositorySortExtensionsTests
{
    private static readonly DateTimeOffset Oldest = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Middle = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newest = new(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly PacmanRepository[] Repositories =
    [
        new() { Name = "alpha", Architecture = "x86_64", CreatedAt = Middle, UpdatedAt = Newest },
        new() { Name = "bravo", Architecture = "x86_64", CreatedAt = Newest, UpdatedAt = Oldest },
        new() { Name = "charlie", Architecture = "x86_64", CreatedAt = Oldest, UpdatedAt = Middle },
    ];

    // An omitted direction is the point of this fixture: Name defaults to A→Z, and the two dates
    // to newest first.
    [TestCase(RepositorySortField.Name, null, new[] { "alpha", "bravo", "charlie" })]
    [TestCase(RepositorySortField.Created, null, new[] { "bravo", "alpha", "charlie" })]
    [TestCase(RepositorySortField.Updated, null, new[] { "alpha", "charlie", "bravo" })]
    [TestCase(RepositorySortField.Name, SortDirection.Ascending, new[] { "alpha", "bravo", "charlie" })]
    [TestCase(RepositorySortField.Name, SortDirection.Descending, new[] { "charlie", "bravo", "alpha" })]
    [TestCase(RepositorySortField.Created, SortDirection.Ascending, new[] { "charlie", "alpha", "bravo" })]
    [TestCase(RepositorySortField.Created, SortDirection.Descending, new[] { "bravo", "alpha", "charlie" })]
    [TestCase(RepositorySortField.Updated, SortDirection.Ascending, new[] { "bravo", "charlie", "alpha" })]
    [TestCase(RepositorySortField.Updated, SortDirection.Descending, new[] { "alpha", "charlie", "bravo" })]
    public void ApplySort_OrdersByField(
        RepositorySortField sortBy,
        SortDirection? direction,
        string[] expected)
    {
        // Act
        var ordered = Repositories
            .AsQueryable()
            .ApplySort(new SortOptions<RepositorySortField> { SortBy = sortBy, Direction = direction });

        // Assert
        Assert.That(ordered.Select(r => r.Name), Is.EqualTo(expected));
    }

    [Test]
    public void ApplySort_DefaultsToNewestCreatedFirst_WhenNothingIsSupplied()
    {
        // The first member of the enum is the default sort field, and its default direction then
        // decides what an entirely unsorted listing returns.
        // Act
        var ordered = Repositories.AsQueryable().ApplySort(new SortOptions<RepositorySortField>());

        // Assert
        Assert.That(ordered.Select(r => r.Name), Is.EqualTo(new[] { "bravo", "alpha", "charlie" }));
    }

    [Test]
    public void ApplySort_BreaksTiesById()
    {
        // Arrange
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var tied = new PacmanRepository[]
        {
            new() { Id = second, Name = "tied", Architecture = "x86_64", CreatedAt = Oldest, UpdatedAt = Oldest },
            new() { Id = first, Name = "tied", Architecture = "x86_64", CreatedAt = Oldest, UpdatedAt = Oldest },
        };

        // Act
        var ordered = tied
            .AsQueryable()
            .ApplySort(new SortOptions<RepositorySortField> { SortBy = RepositorySortField.Name });

        // Assert
        Assert.That(ordered.Select(r => r.Id), Is.EqualTo(new[] { first, second }));
    }
}
