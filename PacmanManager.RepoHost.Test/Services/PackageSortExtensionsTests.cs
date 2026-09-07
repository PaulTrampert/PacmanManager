using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for the ordering <see cref="PackageSortExtensions.ApplySort"/> applies, and in particular
/// for the direction it picks when the caller supplied none.
/// </summary>
/// <remarks>
/// The fixture data orders the three packages differently by every sortable property, so a case
/// that ordered by the wrong property — or in the wrong direction — cannot accidentally produce the
/// expected sequence.
/// </remarks>
[TestFixture]
public class PackageSortExtensionsTests
{
    private static readonly DateTimeOffset Oldest = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Middle = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newest = new(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly PacmanPackage[] Packages =
    [
        Package("alpha", createdAt: Middle, updatedAt: Newest, installedSize: 100),
        Package("bravo", createdAt: Newest, updatedAt: Oldest, installedSize: 200),
        Package("charlie", createdAt: Oldest, updatedAt: Middle, installedSize: 300),
    ];

    // An omitted direction is the point of this fixture: Name defaults to A→Z, and the dates and the
    // size to newest and biggest first.
    [TestCase(PackageSortField.Name, null, new[] { "alpha", "bravo", "charlie" })]
    [TestCase(PackageSortField.Updated, null, new[] { "alpha", "charlie", "bravo" })]
    [TestCase(PackageSortField.Created, null, new[] { "bravo", "alpha", "charlie" })]
    [TestCase(PackageSortField.InstalledSize, null, new[] { "charlie", "bravo", "alpha" })]
    [TestCase(PackageSortField.Name, SortDirection.Ascending, new[] { "alpha", "bravo", "charlie" })]
    [TestCase(PackageSortField.Name, SortDirection.Descending, new[] { "charlie", "bravo", "alpha" })]
    [TestCase(PackageSortField.Updated, SortDirection.Ascending, new[] { "bravo", "charlie", "alpha" })]
    [TestCase(PackageSortField.Updated, SortDirection.Descending, new[] { "alpha", "charlie", "bravo" })]
    [TestCase(PackageSortField.Created, SortDirection.Ascending, new[] { "charlie", "alpha", "bravo" })]
    [TestCase(PackageSortField.Created, SortDirection.Descending, new[] { "bravo", "alpha", "charlie" })]
    [TestCase(PackageSortField.InstalledSize, SortDirection.Ascending, new[] { "alpha", "bravo", "charlie" })]
    [TestCase(PackageSortField.InstalledSize, SortDirection.Descending, new[] { "charlie", "bravo", "alpha" })]
    public void ApplySort_OrdersByField(PackageSortField sortBy, SortDirection? direction, string[] expected)
    {
        // Act
        var ordered = Packages
            .AsQueryable()
            .ApplySort(new SortOptions<PackageSortField> { SortBy = sortBy, Direction = direction });

        // Assert
        Assert.That(ordered.Select(p => p.Name), Is.EqualTo(expected));
    }

    [Test]
    public void ApplySort_DefaultsToNameAscending_WhenNothingIsSupplied()
    {
        // The first member of the enum is the default sort field, and its default direction then
        // decides what an entirely unsorted listing returns: Name, and so A→Z. The fixture is
        // ordered differently by every other property, so this cannot pass by accident.
        // Act
        var ordered = Packages.AsQueryable().ApplySort(new SortOptions<PackageSortField>());

        // Assert
        Assert.That(ordered.Select(p => p.Name), Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));
    }

    [Test]
    public void ApplySort_BreaksTiesById()
    {
        // Arrange
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var tied = new[]
        {
            Package("tied", Oldest, Oldest, 1) with { Id = second },
            Package("tied", Oldest, Oldest, 1) with { Id = first },
        };

        // Act
        var ordered = tied
            .AsQueryable()
            .ApplySort(new SortOptions<PackageSortField> { SortBy = PackageSortField.Name });

        // Assert
        Assert.That(ordered.Select(p => p.Id), Is.EqualTo(new[] { first, second }));
    }

    private static PacmanPackage Package(
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long installedSize) => new()
    {
        Name = name,
        Version = "1.0.0-1",
        Architecture = "x86_64",
        FileName = $"{name}-1.0.0-1-x86_64.pkg.tar.zst",
        Sha256Sum = new string('a', 64),
        Md5Sum = new string('b', 32),
        InstalledSize = installedSize,
        CreatedAt = createdAt,
        UpdatedAt = updatedAt,
    };
}
