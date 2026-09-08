using System.Security.Cryptography;
using LibAlpmSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PTrampert.QueryObjects;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IPackageService"/> backed by the application database, the package
/// file tree and the pacman CLI tools.
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
/// libalpm arrives as a <see cref="Lazy{T}"/> because only publishing needs it. Constructing an
/// <see cref="ILibAlpm"/> regenerates and parses the pacman configuration, expands the
/// per-repository <c>.conf</c> glob and calls <c>alpm_initialize</c>, and every request that
/// resolves the packages controller would otherwise pay for that — including the anonymous read
/// routes, which never load a package file. Worse, it made one malformed <c>.conf</c> enough to
/// turn every listing into a 500. <see cref="LoadPackage"/> is the only member allowed to force
/// the value, and <c>PackageServiceTests</c> asserts the reads leave it uncreated.
/// </para>
/// </remarks>
internal class PackageService(
    PacmanManagerDbContext dbContext,
    IActorAccessor actorAccessor,
    RepositoryAccessPolicy accessPolicy,
    PackageAccessPolicy packagePolicy,
    ICliToolRunner cliRunner,
    IFileSystem fileSystem,
    IPackagePathResolver pathResolver,
    IRepositoryDatabaseLock databaseLock,
    Lazy<ILibAlpm> libAlpm,
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
    /// The repositories the current actor is allowed to see. The only definition of repository
    /// visibility this class has.
    /// </summary>
    private async ValueTask<IQueryable<PacmanRepository>> VisibleRepositoriesAsync(CancellationToken cancellationToken)
    {
        var actor = await actorAccessor.GetActorAsync(cancellationToken);
        return dbContext.PacmanRepositories.Where(accessPolicy.VisibleTo(actor));
    }

    /// <summary>
    /// The packages the current actor is allowed to see. Every other method in this class starts
    /// here.
    /// </summary>
    /// <remarks>
    /// Package visibility is repository visibility, so this composes
    /// <see cref="RepositoryAccessPolicy.VisibleTo"/> as a semi-join rather than restating it as a
    /// predicate over the package. Rewriting it — <c>p.Repository.IsPublic || p.Repository.OwnerId
    /// == userId</c> — would be a second copy of the rule to keep in step with the first, which is
    /// the thing this design goes out of its way to avoid.
    /// </remarks>
    private async ValueTask<IQueryable<PacmanPackage>> VisibleAsync(CancellationToken cancellationToken)
    {
        var visibleRepositories = await VisibleRepositoriesAsync(cancellationToken);
        return dbContext.PacmanPackages.Where(p => visibleRepositories.Any(r => r.Id == p.RepositoryId));
    }

    public async Task<PaginatedResponse<Package>> GetPackagesAsync(
        PaginationParams paginationParams,
        PackageFilter? filter = null,
        SortOptions<PackageSortField>? sort = null,
        CancellationToken cancellationToken = default)
    {
        // Visibility comes first and the caller's criteria are ANDed onto it, so a filter can only
        // ever remove rows from the visible set.
        var query = (await VisibleAsync(cancellationToken))
            .Where(filter ?? new PackageFilter());

        var total = await query.CountAsync(cancellationToken);
        var results = await query
            .ApplySort(sort ?? new SortOptions<PackageSortField>())
            .Skip(paginationParams.Offset)
            .Take(paginationParams.PageSize)
            .Select(Package.Projection)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<Package>
        {
            Results = results,
            Offset = paginationParams.Offset,
            Total = total
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

        // The unique index over (RepositoryId, Name) is exactly this pair, so it matches at most one
        // row and needs no tie-break.
        return await visible
            .Where(p => p.RepositoryId == repositoryId && p.Name == name)
            .Select(Package.Projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PublishPackageResult?> PublishPackageAsync(
        Guid repositoryId,
        Stream packageContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageContent);

        // Step 1: authorize before a single byte of the body is read, so an unauthorized caller is
        // turned away without uploading megabytes first.
        var repository = await LoadRepositoryForPublishAsync(repositoryId, cancellationToken);
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
            return await StoreUploadAsync(repository, publisher, uploadPath, upload, cancellationToken);
        }
        finally
        {
            // Nothing left in the temporary directory, whether the publish succeeded (the file was
            // moved out from under this path), failed, or the upload itself was cut short.
            DiscardTemporaryUpload(uploadPath);
        }
    }

    /// <summary>
    /// Loads the repository being published into, translating the policy outcome into the result or
    /// exception the caller expects.
    /// </summary>
    /// <returns>The tracked entity, or null when the actor must be told it does not exist.</returns>
    private async Task<PacmanRepository?> LoadRepositoryForPublishAsync(
        Guid repositoryId,
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
        return packagePolicy.CheckPublish(repository, actor) switch
        {
            RepositoryAccess.Allowed => repository,
            RepositoryAccess.NotFound => null,
            RepositoryAccess.Unauthenticated => throw new NoCurrentUserException(),
            _ => throw new PackageForbiddenException(repositoryId),
        };
    }

    /// <summary>
    /// What one pass over the uploaded bytes produced.
    /// </summary>
    /// <param name="Sha256Sum">Hex encoded SHA-256 of the bytes received.</param>
    /// <param name="Md5Sum">Hex encoded MD5 of the same bytes.</param>
    /// <param name="ByteCount">How many bytes were received.</param>
    private sealed record UploadedFile(string Sha256Sum, string Md5Sum, long ByteCount);

    /// <summary>
    /// Copies <paramref name="source"/> to <paramref name="destinationPath"/>, computing both
    /// checksums and the byte count from the same pass.
    /// </summary>
    /// <remarks>
    /// The checksums are computed rather than read back from libalpm on purpose. libalpm fills its
    /// checksum fields from a sync database entry, and a package loaded off disk has no such entry,
    /// so both come back null — reading them would store a null for every package this API accepts.
    /// The SHA-256 is the one that has to match the repository database, since <c>repo-add</c>
    /// records it there and a pacman client verifies against it. Pacman 7's <c>repo-add</c> no
    /// longer writes <c>%MD5SUM%</c> at all; the MD5 is kept because this pass produces it for free
    /// and older tooling still asks for it.
    /// </remarks>
    private async Task<UploadedFile> WriteAndHashAsync(
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

        return new UploadedFile(
            Convert.ToHexStringLower(sha256.GetHashAndReset()),
            Convert.ToHexStringLower(md5.GetHashAndReset()),
            byteCount);
    }

    /// <summary>
    /// Extracts and validates the uploaded file's metadata, stages the row, and then performs the
    /// side effects and the commit under the repository's lock.
    /// </summary>
    private async Task<PublishPackageResult> StoreUploadAsync(
        PacmanRepository repository,
        User publisher,
        string uploadPath,
        UploadedFile upload,
        CancellationToken cancellationToken)
    {
        // Step 3: everything stored about the package comes out of the file itself, so a file
        // libalpm cannot open is a rejected upload rather than a server error.
        using var loaded = LoadPackage(uploadPath);
        var name = loaded.Name;
        var version = loaded.Version;
        var architecture = loaded.GetArchitecture();

        // Step 4: validate what libalpm reported. DeriveFileName matches the name, version and
        // architecture against the shapes pacman defines before it formats any of them into a path,
        // and sniffs the compression from the bytes rather than trusting a client supplied name.
        RequireServableArchitecture(repository, architecture);

        string fileName;
        await using (var stored = fileSystem.OpenRead(uploadPath))
        {
            fileName = pathResolver.DeriveFileName(name, version, architecture, stored);
        }

        // Everything from here rewrites the repository's database, so it is serialized per
        // repository and the lock is held across the side effects *and* the commit. Publish and
        // delete share this lock: repo-remove mutates the same file repo-add is writing.
        using var _ = await databaseLock.AcquireAsync(repository.Id, cancellationToken);

        // Step 5: stage the row. The upsert key is (repositoryId, name), which is the unique index,
        // so this matches at most one row — and a row already there has to be older than what is
        // being pushed, since a published version's bytes are ones a client may already hold.
        var visible = await VisibleAsync(cancellationToken);
        var existing = await visible
            .SingleOrDefaultAsync(p => p.RepositoryId == repository.Id && p.Name == name, cancellationToken);

        if (existing is not null)
        {
            RequireForwardProgress(existing, version, architecture);
        }

        var now = DateTimeOffset.UtcNow;
        var created = existing is null;
        var previousFileName = existing?.FileName;

        var package = existing ?? new PacmanPackage
        {
            Id = Guid.CreateVersion7(),
            RepositoryId = repository.Id,
            Name = name,
            Version = version,
            Architecture = architecture,
            FileName = fileName,
            Sha256Sum = upload.Sha256Sum,
            Md5Sum = upload.Md5Sum,
            CreatedAt = now,
        };

        ApplyMetadata(package, loaded, fileName, upload, publisher, now);

        if (created)
        {
            // DbContext.AddAsync rather than the DbSet, so that PacmanPackages stays named in
            // exactly one method.
            await dbContext.AddAsync(package, cancellationToken);
        }

        // The repository's contents changed, so its own timestamp moves with them.
        repository.UpdatedAt = now;

        await CommitWithSideEffectsAsync(
            repository,
            package,
            uploadPath,
            previousFileName,
            created,
            cancellationToken);

        // Only now is the replaced file certain to be unreferenced. Until the commit succeeded it
        // was what a rollback restored the database to. The name comparison is belt and braces —
        // a publish moves the version forward, so the two names cannot be equal — but this is the
        // one place that deletes a file a live row could still name, so it checks anyway.
        if (!created && previousFileName is not null && previousFileName != package.FileName)
        {
            DeleteQuietly(pathResolver.GetPackageFilePath(repository.Id, previousFileName),
                "superseded package file");
        }

        return new PublishPackageResult(Package.FromPacmanPackage(package), created);
    }

    /// <summary>
    /// Step 6: move the file into place, run <c>repo-add</c>, then commit — side effects first,
    /// commit last, so that a failed tool costs nothing more than a discarded change tracker.
    /// </summary>
    /// <remarks>
    /// There are two distinct failures to unwind, and they are not the same shape:
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>repo-add</c> failed. The commit was never reached, so the change tracker is discarded
    ///     and no row moved. The file just written is deleted, which cannot take away a file the
    ///     unchanged row still names: a publish has to move the version forward, so the name it
    ///     derives is never the name the previous version is stored under.
    ///   </description></item>
    ///   <item><description>
    ///     The commit failed after <c>repo-add</c> succeeded. The database file now advertises a
    ///     package the rows do not know about, and nothing else would ever notice, so it is undone
    ///     explicitly: <c>repo-remove</c>, then either delete the new file (a new package) or
    ///     <c>repo-add</c> the previous one (a replacement), which is exactly why the previous file
    ///     is not deleted until after the commit.
    ///   </description></item>
    /// </list>
    /// A compensating step that fails itself is logged at error and the original exception is
    /// allowed to surface; the repository database is then genuinely out of step with the rows, and
    /// the cure is reconciliation rather than anything this request can do.
    /// </remarks>
    private async Task CommitWithSideEffectsAsync(
        PacmanRepository repository,
        PacmanPackage package,
        string uploadPath,
        string? previousFileName,
        bool created,
        CancellationToken cancellationToken)
    {
        var destination = pathResolver.GetPackageFilePath(repository.Id, package.FileName);
        var addSucceeded = false;

        try
        {
            fileSystem.CreateDirectory(pathResolver.GetRepositoryDirectory(repository.Id));
            fileSystem.Move(uploadPath, destination, overwrite: true);

            await cliRunner.RunToolCheckedAsync(
                new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath, destination),
                cancellationToken);
            addSucceeded = true;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to publish {PackageName} to repository {RepositoryId}",
                package.Name, repository.Id);

            dbContext.ChangeTracker.Clear();

            try
            {
                if (addSucceeded)
                {
                    await CompensateCommittedAddAsync(
                        repository, package, destination, previousFileName, created);
                }
                else
                {
                    // The previous version, if there was one, is untouched on disk under its own
                    // name and the row still names it, so the repository is exactly as it was.
                    DeleteQuietly(destination, "package file from a failed publish");
                }
            }
            catch (Exception compensation)
            {
                logger.LogError(compensation,
                    "Failed to undo the partial publish of {PackageName} in repository {RepositoryId}. "
                    + "Its database and its rows now disagree.",
                    package.Name, repository.Id);
            }

            throw;
        }
    }

    /// <summary>
    /// Undoes a <c>repo-add</c> that succeeded before a commit that did not.
    /// </summary>
    /// <remarks>
    /// This takes no cancellation token on purpose. One of the ordinary ways the commit fails is
    /// the request being cancelled — the client disconnected while it was in flight — and the
    /// token the publish ran under is then already cancelled, so passing it on would abandon the
    /// cleanup at its first await and leave the database file advertising a package no row
    /// describes. Undoing a side effect is work that has to happen whatever became of the request
    /// that caused it.
    /// </remarks>
    private async Task CompensateCommittedAddAsync(
        PacmanRepository repository,
        PacmanPackage package,
        string destination,
        string? previousFileName,
        bool created)
    {
        await cliRunner.RunToolCheckedAsync(
            new RepoRemove(repository.Id.ToString(), _pacmanConfig.DbPath, package.Name),
            CancellationToken.None);

        // Nothing references the file that was just moved into place: no row was committed, and
        // the entry that named it has just been removed. It is never the previous version's file,
        // since a publish has to move the version forward to get this far.
        DeleteQuietly(destination, "package file from a rolled back publish");

        if (created || previousFileName is null)
        {
            return;
        }

        // The previous file has deliberately not been deleted yet, so putting its entry back is
        // enough to leave the database describing what the rows still say.
        var previousPath = pathResolver.GetPackageFilePath(repository.Id, previousFileName);
        if (!fileSystem.Exists(previousPath))
        {
            return;
        }

        await cliRunner.RunToolCheckedAsync(
            new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath, previousPath),
            CancellationToken.None);
    }

    /// <summary>
    /// Reads the uploaded file's metadata, restating a libalpm failure as the client error it is.
    /// </summary>
    /// <remarks>
    /// The one place libalpm is actually needed, and so the one place that is allowed to force
    /// <c>libAlpm</c>. Touching <see cref="Lazy{T}.Value"/> anywhere else would put the cost of
    /// initializing libalpm back on requests that never read a package file.
    /// </remarks>
    /// <exception cref="UnreadablePackageException">libalpm could not read the file as a package.</exception>
    private IPackage LoadPackage(string uploadPath)
    {
        try
        {
            return libAlpm.Value.LoadPackageFile(uploadPath);
        }
        catch (AlpmException e)
        {
            throw new UnreadablePackageException(e);
        }
    }

    /// <summary>
    /// Rejects a package built for an architecture this repository does not serve.
    /// </summary>
    private static void RequireServableArchitecture(PacmanRepository repository, string architecture)
    {
        if (string.Equals(architecture, repository.Architecture, StringComparison.Ordinal)
            || string.Equals(architecture, PackageArchitectureMismatchException.AnyArchitecture,
                StringComparison.Ordinal))
        {
            return;
        }

        throw new PackageArchitectureMismatchException(architecture, repository.Architecture);
    }

    /// <summary>
    /// Rejects an upload that does not move the package forward, which is what keeps a published
    /// version's bytes the bytes every client that synced this repository was promised.
    /// </summary>
    /// <remarks>
    /// Ordering is <see cref="AlpmVersion"/>'s rather than the string's, because it has to be the
    /// same ordering the pacman client reading this repository will apply — <c>1.10</c> is newer
    /// than <c>1.9</c>, and an epoch outranks everything to its right. A build for a different
    /// architecture is a different package file rather than a re-push of the same one, so it is
    /// allowed to carry the version it was built with.
    /// </remarks>
    /// <exception cref="PackageNotNewerException">
    /// The repository already holds this package, for this architecture, at that version or newer.
    /// </exception>
    private static void RequireForwardProgress(PacmanPackage existing, string version, string architecture)
    {
        if (!string.Equals(existing.Architecture, architecture, StringComparison.Ordinal))
        {
            return;
        }

        if (AlpmVersion.IsNewerThan(version, existing.Version))
        {
            return;
        }

        throw new PackageNotNewerException(existing.Name, existing.Version, version, architecture);
    }

    /// <summary>
    /// Copies everything libalpm reported, plus the computed checksums, onto the row being staged.
    /// </summary>
    /// <remarks>
    /// A replacement is updated in place: its id and creation time are the ones it already had, and
    /// everything else — including the publisher — becomes what was just pushed.
    /// </remarks>
    private static void ApplyMetadata(
        PacmanPackage package,
        IPackage loaded,
        string fileName,
        UploadedFile upload,
        User publisher,
        DateTimeOffset now)
    {
        package.Name = loaded.Name;
        package.Version = loaded.Version;
        package.Description = NullIfEmpty(loaded.Description);
        package.Base = NullIfEmpty(loaded.GetBase());
        package.Url = NullIfEmpty(loaded.GetUrl());
        package.Architecture = loaded.GetArchitecture();
        package.Packager = NullIfEmpty(loaded.GetPackager());
        package.FileName = fileName;

        // The measured byte count rather than libalpm's size, because it is the length of the file
        // actually stored and so cannot disagree with the %CSIZE% repo-add records for it.
        package.CompressedSize = upload.ByteCount;
        package.InstalledSize = loaded.GetInstalledSize();
        package.BuildDate = loaded.GetBuildDate();
        package.Sha256Sum = upload.Sha256Sum;
        package.Md5Sum = upload.Md5Sum;

        package.Licenses = loaded.GetLicenses();
        package.Groups = loaded.GetGroups();
        package.Provides = Spell(loaded.GetProvides());
        package.Replaces = Spell(loaded.GetReplaces());
        package.Depends = Spell(loaded.GetDependencies());
        package.OptDepends = SpellOptional(loaded.GetOptionalDependencies());
        package.MakeDepends = Spell(loaded.GetMakeDepends());
        package.CheckDepends = Spell(loaded.GetCheckDepends());
        package.Conflicts = Spell(loaded.GetConflicts());

        package.PublisherId = publisher.Id;
        package.Publisher = publisher;
        package.UpdatedAt = now;
    }

    /// <summary>
    /// Renders dependencies in pacman's own spelling (<c>foo&gt;=1.2</c>), which round-trips
    /// losslessly as text and is what a reader of these columns expects to see.
    /// </summary>
    private static List<string> Spell(IEnumerable<AlpmDependency> dependencies) =>
        dependencies.Select(d => d.ToString()).ToList();

    /// <summary>
    /// Renders optional dependencies, which carry pacman's <c>name: reason</c> spelling that
    /// <see cref="AlpmDependency.ToString"/> does not include.
    /// </summary>
    private static List<string> SpellOptional(IEnumerable<AlpmDependency> dependencies) =>
        dependencies
            .Select(d => string.IsNullOrEmpty(d.Description) ? d.ToString() : $"{d}: {d.Description}")
            .ToList();

    /// <summary>
    /// Treats libalpm's empty strings as the absent values the nullable columns mean.
    /// </summary>
    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

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

        // The same unique index the read path uses, so this pair matches at most one row.
        var package = await visible
            .SingleOrDefaultAsync(p => p.RepositoryId == repositoryId && p.Name == name, cancellationToken);

        return await DeleteResolvedPackageAsync(package, cancellationToken);
    }

    /// <summary>
    /// Where both delete routes converge: authorize the package that was resolved from the visible
    /// set, then remove it under the repository's lock.
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

        // The permission is held over the repository, not over the package, so this is the same
        // check publishing makes and it is made against the same tracked entity whose UpdatedAt the
        // delete moves. Whoever published the package is irrelevant to it.
        var repository = await LoadRepositoryForPublishAsync(package.RepositoryId, cancellationToken);
        if (repository is null)
        {
            return false;
        }

        // Publish and delete take the same lock: repo-remove rewrites the file repo-add writes, and
        // it is held across the side effect *and* the commit so no other writer sees the window in
        // which the database file and the rows disagree.
        using var _ = await databaseLock.AcquireAsync(repository.Id, cancellationToken);

        await RemoveWithSideEffectsAsync(repository, package, cancellationToken);
        return true;
    }

    /// <summary>
    /// Runs <c>repo-remove</c>, then commits, then deletes the file — publishing's ordering, for
    /// publishing's reason.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>repo-remove</c> runs first and outside the commit's try block, so a tool failure aborts
    ///     the request with nothing staged and nothing written. The package stays listed and stays
    ///     installable, which is the recoverable state: the alternative ordering deletes a row whose
    ///     entry is still in the database a pacman client syncs, and nothing would ever notice.
    ///   </description></item>
    ///   <item><description>
    ///     The commit is second. If it fails, the database file no longer advertises a package the
    ///     rows still describe, so it is undone explicitly by adding the file back — which is
    ///     possible only because the file has not been deleted yet.
    ///   </description></item>
    ///   <item><description>
    ///     Deleting the file is last, and is the one step that may fail harmlessly. The row is the
    ///     source of truth and it is already gone, so a file that cannot be removed is an inert
    ///     orphan worth a warning and never worth failing the request over.
    ///   </description></item>
    /// </list>
    /// A compensating step that fails itself is logged at error and the original exception is
    /// allowed to surface, as in publishing: the database file and the rows are then genuinely out
    /// of step and the cure is reconciliation rather than anything this request can do.
    /// </remarks>
    private async Task RemoveWithSideEffectsAsync(
        PacmanRepository repository,
        PacmanPackage package,
        CancellationToken cancellationToken)
    {
        // Resolved before the row is detached, since the file name is read off the row.
        var packageFilePath = pathResolver.GetPackageFilePath(repository.Id, package.FileName);

        await cliRunner.RunToolCheckedAsync(
            new RepoRemove(repository.Id.ToString(), _pacmanConfig.DbPath, package.Name),
            cancellationToken);

        try
        {
            // DbContext.Remove rather than the DbSet, so that PacmanPackages stays named in exactly
            // one method.
            dbContext.Remove(package);

            // The repository's contents changed, so its own timestamp moves with them.
            repository.UpdatedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete {PackageName} from repository {RepositoryId}",
                package.Name, repository.Id);

            dbContext.ChangeTracker.Clear();

            try
            {
                await CompensateCommittedRemoveAsync(repository, package, packageFilePath);
            }
            catch (Exception compensation)
            {
                logger.LogError(compensation,
                    "Failed to undo the partial deletion of {PackageName} in repository {RepositoryId}. "
                    + "Its database and its rows now disagree.",
                    package.Name, repository.Id);
            }

            throw;
        }

        // Only now is the file certain to be unreferenced: the entry that named it is gone and so is
        // the row. Until the commit succeeded it was what the compensation restored the database
        // from.
        DeleteQuietly(packageFilePath, "package file of a deleted package");
    }

    /// <summary>
    /// Undoes a <c>repo-remove</c> that succeeded before a commit that did not, by adding the
    /// package file back to the repository's database.
    /// </summary>
    /// <remarks>
    /// This takes no cancellation token, for the reason
    /// <see cref="CompensateCommittedAddAsync"/> does not: one of the ordinary ways the commit
    /// fails is the request being cancelled, and a compensation running on the cancelled token
    /// would abandon itself at its first await and leave the database file missing a package the
    /// rows still describe.
    /// </remarks>
    private async Task CompensateCommittedRemoveAsync(
        PacmanRepository repository,
        PacmanPackage package,
        string packageFilePath)
    {
        // The file is deleted only after a successful commit, so it is still here. If it is not,
        // there is nothing to rebuild the entry from and saying so is the most this can do.
        if (!fileSystem.Exists(packageFilePath))
        {
            logger.LogError(
                "Cannot restore the database entry for {PackageName} in repository {RepositoryId}: "
                + "its file is no longer at {Path}.",
                package.Name, repository.Id, packageFilePath);
            return;
        }

        await cliRunner.RunToolCheckedAsync(
            new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath, packageFilePath),
            CancellationToken.None);
    }
}
