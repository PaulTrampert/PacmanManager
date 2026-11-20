using PacmanManager.Entities;

namespace PacmanManager.Packages;

/// <summary>
/// The outcome of a publish that committed.
/// </summary>
/// <param name="Package">The package as stored, its publisher loaded.</param>
/// <param name="Created">
/// <c>true</c> when the package is new to its repository, <c>false</c> when it replaced a build of
/// the same name and architecture, whose id and creation time it kept.
/// </param>
public sealed record PublishedPackage(PacmanPackage Package, bool Created);
