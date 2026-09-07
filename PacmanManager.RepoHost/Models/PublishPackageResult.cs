namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The outcome of publishing a package: the stored package, and whether it was new to its
/// repository.
/// </summary>
/// <remarks>
/// Publishing is an upsert on <c>(repositoryId, name)</c>, and a repository holds exactly one
/// version of a package, so replacing an existing one is the normal path rather than an edge case.
/// The controller needs to tell the two apart to answer <c>201</c> or <c>200</c>, and nothing on
/// the package itself says which happened.
/// </remarks>
/// <param name="Package">The package as it now stands.</param>
/// <param name="Created">
/// True when the package was not previously in this repository, which is a <c>201</c>; false when
/// an existing package was replaced, which is a <c>200</c>.
/// </param>
public record PublishPackageResult(Package Package, bool Created);
