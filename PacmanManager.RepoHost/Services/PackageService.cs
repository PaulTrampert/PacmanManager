using PacmanManager.Authentication;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PacmanManager.Entities;
using PacmanManager.Packages;
using PacmanManager.Packages.Authorization;
using PacmanManager.Packages.Infrastructure;
using PacmanManager.Packages.LibAlpm;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PTrampert.QueryObjects;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IPackageService"/> backed by the application database, the package
/// file tree and, for every change, the <see cref="IPackagePublisher"/>.
/// </summary>
/// <remarks>
/// <para>
/// Authorization is enforced structurally rather than by remembering to check, exactly as
/// <see cref="RepositoryService"/> does it. Every read starts from <see cref="VisibleAsync"/>, which
/// is the only place in this class that touches
/// <see cref="PacmanManagerDbContext.PacmanPackages"/>. A method that forgets the rules therefore
/// has to name the <see cref="DbSet{TEntity}"/> to do so, which is both obvious in review and caught
/// by <c>PackageServiceEnforcementTests</c>.
/// </para>
/// <para>
/// A change is authorized here and then handed to the <see cref="IPackagePublisher"/>, which checks
/// no permissions of its own and performs the side effects and the commit under the repository's
/// lock. It reads the upload through libalpm lazily, so the read paths never initialize libalpm.
/// </para>
/// </remarks>
internal class PackageService(
    PacmanManagerDbContext dbContext,
    IActorAccessor actorAccessor,
    PackageAccessPolicy packagePolicy,
    IPackagePublisher packagePublisher,
    IFileSystem fileSystem,
    IPackagePathResolver pathResolver,
    IOptions<PacmanConfigSettings> pacmanSettings,
    ILogger<PackageService> logger) : IPackageService
{
    /// <summary>
    /// How much of an upload is hashed and written at a time. Large enough that a hundreds of
    /// megabytes upload is not a million round trips, small enough to stay off the large object
    /// heap.
    /// </summary>
    private const int UploadBufferSize = 64 * 1024;

    private readonly PacmanConfigSettings _pacmanConfig = pacmanSettings.Value;

    /// <summary>
    /// The repositories whose packages the current actor is allowed to see. The only definition of
    /// package visibility this class has.
    /// </summary>
    private async ValueTask<IQueryable<PacmanRepository>> VisibleRepositoriesAsync(CancellationToken cancellationToken)
    {
        var actor = await actorAccessor.GetActorAsync(cancellationToken);
        return dbContext.PacmanRepositories.Where(packagePolicy.VisibleTo(actor));
    }

    /// <summary>
    /// The packages the current actor is allowed to see. Every other method in this class starts
    /// here.
    /// </summary>
    /// <remarks>
    /// Package visibility is repository visibility, so this composes
    /// <see cref="PackageAccessPolicy.VisibleTo"/>, which is built on
    /// <see cref="RepositoryAccessPolicy.VisibleTo"/>, as a semi-join rather than restating it as a
    /// predicate over the package. Rewriting it — <c>p.Repository.IsPublic || p.Repository.OwnerId
    /// == userId</c> — would be a second copy of the rule to keep in step with the first, which is
    /// the thing this design goes out of its way to avoid.
    /// </remarks>
    private async ValueTask<IQueryable<PacmanPackage>> VisibleAsync(CancellationToken cancellationToken)
    {
        var visibleRepositories = await VisibleRepositoriesAsync(cancellationToken);
        return dbContext.PacmanPackages.Where(p => visibleRepositories.Any(r => r.Id == p.RepositoryId));
    }

    public async Task<PackagePaginatedResponse> GetPackagesAsync(
        PaginationParams paginationParams,
        PackageFilter? filter = null,
        SortOptions<PackageSortField>? sort = null,
        CancellationToken cancellationToken = default)
    {
        // Visibility comes first and the caller's criteria are ANDed onto it, so a filter can only
        // ever remove rows from the visible set.
        var query = (await VisibleAsync(cancellationToken))
            .Where(filter ?? new PackageFilter());

        var sortOptions = sort ?? new SortOptions<PackageSortField>();
        var total = await query.CountAsync(cancellationToken);
        var results = await query
            .ApplySort(sortOptions)
            .Skip(paginationParams.Offset)
            .Take(paginationParams.PageSize)
            .Select(Package.Projection)
            .ToListAsync(cancellationToken);

        return new PackagePaginatedResponse
        {
            Results = results,
            Offset = paginationParams.Offset,
            Total = total,
            Sort = sortOptions.Resolve()
        };
    }

    public async Task<Package?> GetPackageByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var visible = await VisibleAsync(cancellationToken);
        return await visible
            .Where(p => p.Id == id)
            .Select(Package.Projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Package?> GetPackageByNameAsync(
        Guid repositoryId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var visible = await VisibleAsync(cancellationToken);

        // The unique index is (RepositoryId, Name, Architecture), but an any build cannot sit beside
        // an architecture specific one of the same name, and the only architecture a repository may
        // support today is x86_64, so a name still matches at most one row. Addressing a package by
        // name in a repository holding several architecture specific builds of it is part of
        // supporting other architectures, not something this lookup guesses at.
        return await visible
            .Where(p => p.RepositoryId == repositoryId && p.Name == name)
            .Select(Package.Projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PackageContent?> GetPackageContentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Resolve first, then open: the lookup already applies the visibility rules, so nothing
        // here has to restate them, and a package the actor may not see never reaches the disk.
        var package = await GetPackageByIdAsync(id, cancellationToken);
        return package is null ? null : OpenContent(package);
    }

    public async Task<PackageContent?> GetPackageContentByNameAsync(
        Guid repositoryId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var package = await GetPackageByNameAsync(repositoryId, name, cancellationToken);
        return package is null ? null : OpenContent(package);
    }

    /// <summary>
    /// Opens the stored file of a package that has already been resolved from the visible set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The path is composed from the package's own repository id and the basename its row records.
    /// Both were derived by the publish that stored the file and neither came from a request, so
    /// nothing a client supplied ever reaches
    /// <see cref="IPackagePathResolver.GetPackageFilePath"/> — which is also why the caller's
    /// <c>name</c> is used to find a row and never to build a path.
    /// </para>
    /// <para>
    /// A row whose file is missing is deliberately not a <c>404</c>. The rows are the source of
    /// truth for what a repository holds: a package that is listed, that a client can read a
    /// checksum for, and whose bytes are gone is a fault in the store rather than an answer to the
    /// question the caller asked. Reporting it as absent would hide it behind a status that reads
    /// as ordinary — the same status a private repository produces — and leave the operator with
    /// nothing to notice. So it is logged at error and surfaces as a <c>500</c>, which is what an
    /// inconsistent store deserves.
    /// </para>
    /// </remarks>
    /// <exception cref="FileNotFoundException">The row exists but its file does not.</exception>
    private PackageContent OpenContent(Package package)
    {
        var path = pathResolver.GetPackageFilePath(package.RepositoryId, package.FileName);

        if (!fileSystem.Exists(path))
        {
            logger.LogError(
                "Package {PackageId} in repository {RepositoryId} names {FileName}, which is not on disk at {Path}",
                package.Id, package.RepositoryId, package.FileName, path);

            throw new FileNotFoundException(
                $"The stored file for package {package.Id} is missing.", path);
        }

        return new PackageContent(fileSystem.OpenRead(path), package.FileName);
    }

    public async Task<PublishPackageResult?> PublishPackageAsync(
        Guid repositoryId,
        Stream packageContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageContent);

        // Step 1: authorize before a single byte of the body is read, so an unauthorized caller is
        // turned away without uploading megabytes first.
        var repository = await LoadRepositoryForChangeAsync(repositoryId, packagePolicy.CheckPublish, cancellationToken);
        if (repository is null)
        {
            return null;
        }

        var actor = await actorAccessor.GetActorAsync(cancellationToken);
        var publisher = actor.User ?? throw new NoCurrentUserException();

        // Step 2: stream the body to a temporary file, hashing as it goes. One pass over the bytes
        // produces both checksums and the compressed size, and nothing is held in memory.
        fileSystem.CreateDirectory(_pacmanConfig.TmpDir);
        var uploadPath = Path.Combine(_pacmanConfig.TmpDir, $"{Guid.CreateVersion7()}.upload");
        try
        {
            var upload = await WriteAndHashAsync(packageContent, uploadPath, cancellationToken);
            var published = await packagePublisher.PublishAsync(repository, publisher, upload, cancellationToken);
            return new PublishPackageResult(Package.FromPacmanPackage(published.Package), published.Created);
        }
        finally
        {
            // Nothing left in the temporary directory, whether the publish succeeded (the file was
            // moved out from under this path), failed, or the upload itself was cut short.
            DiscardTemporaryUpload(uploadPath);
        }
    }

    /// <summary>
    /// Loads the repository whose packages are changing, translating the outcome of
    /// <paramref name="verdict"/> into the result or exception the caller expects.
    /// </summary>
    /// <param name="repositoryId">The repository to load.</param>
    /// <param name="verdict">
    /// The <see cref="PackageAccessPolicy"/> verdict for the change being made, so that the
    /// operation, not this helper, decides which question is asked.
    /// </param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The tracked entity, or null when the actor must be told it does not exist.</returns>
    private async Task<PacmanRepository?> LoadRepositoryForChangeAsync(
        Guid repositoryId,
        Func<PacmanRepository, Actor, RepositoryAccess> verdict,
        CancellationToken cancellationToken)
    {
        var visible = await VisibleRepositoriesAsync(cancellationToken);
        var repository = await visible.SingleOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);

        if (repository is null)
        {
            // Either it genuinely does not exist, or it is private and belongs to someone else.
            // Both cases have to look identical from the outside.
            return null;
        }

        var actor = await actorAccessor.GetActorAsync(cancellationToken);
        return verdict(repository, actor) switch
        {
            RepositoryAccess.Allowed => repository,
            RepositoryAccess.NotFound => null,
            RepositoryAccess.Unauthenticated => throw new NoCurrentUserException(),
            _ => throw new PackageForbiddenException(repositoryId),
        };
    }

    /// <summary>
    /// Copies <paramref name="source"/> to <paramref name="destinationPath"/>, computing both
    /// checksums and the byte count from the same pass.
    /// </summary>
    /// <remarks>
    /// <see cref="PackageUpload"/> says why the checksums are computed rather than read back from
    /// libalpm.
    /// </remarks>
    private async Task<PackageUpload> WriteAndHashAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        var buffer = new byte[UploadBufferSize];
        long byteCount = 0;

        await using (var destination = fileSystem.OpenWrite(destinationPath))
        {
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                sha256.AppendData(buffer, 0, read);
                md5.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                byteCount += read;
            }

            await destination.FlushAsync(cancellationToken);
        }

        return new PackageUpload(
            destinationPath,
            Convert.ToHexStringLower(sha256.GetHashAndReset()),
            Convert.ToHexStringLower(md5.GetHashAndReset()),
            byteCount);
    }

    /// <summary>
    /// Removes the temporary upload if it is still there. A successful publish moved it away, so
    /// most of the time this finds nothing.
    /// </summary>
    private void DiscardTemporaryUpload(string uploadPath) => DeleteQuietly(uploadPath, "temporary upload");

    /// <summary>
    /// Deletes a file that nothing points at any more. A file left behind is inert, so failing to
    /// remove one is worth a warning and never worth failing or masking the operation that led
    /// here.
    /// </summary>
    private void DeleteQuietly(string path, string description)
    {
        try
        {
            if (fileSystem.Exists(path))
            {
                fileSystem.Delete(path);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not remove {Description} at {Path}", description, path);
        }
    }

    public async Task<bool> DeletePackageAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        var visible = await VisibleAsync(cancellationToken);
        var package = await visible.SingleOrDefaultAsync(p => p.Id == packageId, cancellationToken);

        return await DeleteResolvedPackageAsync(package, cancellationToken);
    }

    public async Task<bool> DeletePackageAsync(
        Guid repositoryId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var visible = await VisibleAsync(cancellationToken);

        // The same lookup the read path uses, so this pair matches at most one row for the reason
        // GetPackageByNameAsync gives.
        var package = await visible
            .SingleOrDefaultAsync(p => p.RepositoryId == repositoryId && p.Name == name, cancellationToken);

        return await DeleteResolvedPackageAsync(package, cancellationToken);
    }

    /// <summary>
    /// Where both delete routes converge: authorize the package that was resolved from the visible
    /// set, then have the publisher remove it under the repository's lock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resolution and the permission check both happen before the lock is taken, so a caller who
    /// may not delete never waits behind a publish to be told so, exactly as publishing authorizes
    /// before it reads a byte of the body.
    /// </para>
    /// <para>
    /// A package that is not in the visible set is reported as missing rather than forbidden, which
    /// covers the three cases that have to look identical from outside: it does not exist, it was
    /// already deleted, and it is in somebody else's private repository.
    /// </para>
    /// </remarks>
    /// <returns><c>false</c> when the actor must be told the package is not there.</returns>
    private async Task<bool> DeleteResolvedPackageAsync(PacmanPackage? package, CancellationToken cancellationToken)
    {
        if (package is null)
        {
            return false;
        }

        // The permission is held over the repository, not over the package, so the check is made
        // against the same tracked entity whose UpdatedAt the delete moves. Whoever published the
        // package is irrelevant to it.
        var repository = await LoadRepositoryForChangeAsync(
            package.RepositoryId,
            packagePolicy.CheckDelete,
            cancellationToken);
        if (repository is null)
        {
            return false;
        }

        // The publisher takes the repository's lock, which publishing takes too.
        await packagePublisher.RemoveAsync(repository, package, cancellationToken);
        return true;
    }
}
