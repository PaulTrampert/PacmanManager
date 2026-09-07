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
/// Authorization is enforced structurally rather than by remembering to check, exactly as
/// <see cref="RepositoryService"/> does it. Every read starts from <see cref="VisibleAsync"/>, which
/// is the only place in this class that touches
/// <see cref="PacmanManagerDbContext.PacmanPackages"/>. A method that forgets the rules therefore
/// has to name the <see cref="DbSet{TEntity}"/> to do so, which is both obvious in review and caught
/// by <c>PackageServiceEnforcementTests</c>.
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
    ILibAlpm libAlpm,
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

            await RunDatabaseToolAsync(
                new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath, destination),
                repository.Id,
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
        await RunDatabaseToolAsync(
            new RepoRemove(repository.Id.ToString(), _pacmanConfig.DbPath, package.Name),
            repository.Id,
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

        await RunDatabaseToolAsync(
            new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath, previousPath),
            repository.Id,
            CancellationToken.None);
    }

    /// <summary>
    /// Runs one of the pacman database tools and turns a non-zero exit into an exception, since the
    /// runner itself only reports the code.
    /// </summary>
    /// <remarks>
    /// Every failure is the same failure: the repository database did not change, and what the
    /// caller can do about it does not depend on why. The tools distinguish their reasons only in
    /// prose — there is no exit code for a lock file it could not take — so guessing at the reason
    /// from the message would only be right some of the time, and a wrong guess (a package whose
    /// name happens to contain the word) would advise a client to retry something that will never
    /// succeed. The diagnostics are logged where an operator will see them and the exception
    /// carries them too.
    /// </remarks>
    /// <exception cref="RepositoryDatabaseToolException">The tool exited non-zero.</exception>
    private async Task RunDatabaseToolAsync(ICliTool tool, Guid repositoryId, CancellationToken cancellationToken)
    {
        var output = new CollectingCliOutputHandler();
        var exitCode = await cliRunner.RunToolAsync(tool, output, cancellationToken);
        if (exitCode == 0)
        {
            return;
        }

        var diagnostics = string.IsNullOrWhiteSpace(output.StdErr) ? output.StdOut : output.StdErr;
        logger.LogError(
            "'{Tool}' exited with code {ExitCode} against repository {RepositoryId}: {Diagnostics}",
            tool.Name, exitCode, repositoryId, diagnostics);

        throw new RepositoryDatabaseToolException(tool.Name, exitCode, diagnostics);
    }

    /// <summary>
    /// Reads the uploaded file's metadata, restating a libalpm failure as the client error it is.
    /// </summary>
    /// <exception cref="UnreadablePackageException">libalpm could not read the file as a package.</exception>
    private IPackage LoadPackage(string uploadPath)
    {
        try
        {
            return libAlpm.LoadPackageFile(uploadPath);
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
}
