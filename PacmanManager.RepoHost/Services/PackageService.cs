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

        // Step 5: stage the row. The upsert key is (repositoryId, name, architecture), which is the
        // unique index, so at most one row shares the new build's architecture. Every build of the
        // name is loaded, not just that one, because an any build and an architecture specific
        // build would be listed in the same database: whichever is published replaces the other.
        var visible = await VisibleAsync(cancellationToken);
        var sameName = await visible
            .Where(p => p.RepositoryId == repository.Id && p.Name == name)
            .ToListAsync(cancellationToken);

        var replaced = sameName.Where(p => Replaces(architecture, p.Architecture)).ToList();

        // Each replaced build has to be older than what is being pushed, since a published
        // version's bytes are ones a client may already hold.
        foreach (var previous in replaced)
        {
            RequireForwardProgress(previous, version);
        }

        var existing = replaced.SingleOrDefault(p => string.Equals(p.Architecture, architecture, StringComparison.Ordinal));
        var now = DateTimeOffset.UtcNow;
        var created = existing is null;

        // Captured before ApplyMetadata rewrites the upserted row in place.
        var superseded = replaced.Select(p => new SupersededBuild(p.Architecture, p.FileName)).ToList();

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

        // Builds for another architecture that this one replaces are gone once it is committed.
        foreach (var other in replaced.Where(p => !ReferenceEquals(p, existing)))
        {
            dbContext.Remove(other);
        }

        // The repository's contents changed, so its own timestamp moves with them.
        repository.UpdatedAt = now;

        await CommitWithSideEffectsAsync(
            repository,
            package,
            uploadPath,
            superseded,
            cancellationToken);

        // Only now are the replaced files certain to be unreferenced. Until the commit succeeded
        // they were what a rollback restored the databases from. The name comparison is belt and
        // braces — a publish moves the version forward, and a build for another architecture
        // carries that architecture in its name, so the names cannot be equal — but this is the one
        // place that deletes a file a live row could still name, so it checks anyway.
        foreach (var previous in superseded.Where(b => b.FileName != package.FileName))
        {
            DeleteQuietly(pathResolver.GetPackageFilePath(repository.Id, previous.FileName),
                "superseded package file");
        }

        return new PublishPackageResult(Package.FromPacmanPackage(package), created);
    }

    /// <summary>
    /// A build a publish replaces, as it was before the publish touched anything.
    /// </summary>
    /// <param name="Architecture">The architecture it was built for.</param>
    /// <param name="FileName">The basename of its stored file.</param>
    private sealed record SupersededBuild(string Architecture, string FileName);

    /// <summary>
    /// Whether a build for <paramref name="offered"/> replaces an existing build of the same name
    /// for <paramref name="published"/>.
    /// </summary>
    /// <remarks>
    /// A build replaces the one for its own architecture. Beyond that, an <c>any</c> build is listed
    /// in every supported architecture's database, so it and an architecture specific build of the
    /// same name cannot both be published: an <c>any</c> build replaces every architecture specific
    /// one, and an architecture specific build replaces the <c>any</c> one. A package that needs to
    /// differ on one architecture is packaged explicitly for each supported architecture. Builds
    /// for two different specific architectures never share a database, so neither replaces the
    /// other.
    /// </remarks>
    private static bool Replaces(string offered, string published) =>
        string.Equals(offered, published, StringComparison.Ordinal) || IsAny(offered) || IsAny(published);

    /// <summary>
    /// Step 6: move the file into place, run <c>repo-add</c> once for each database the package
    /// belongs in, take whatever it replaces out of the databases the new build is not listed in,
    /// then commit — side effects first, commit last, so that a failed tool costs nothing more than
    /// a discarded change tracker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file is stored once however many databases list it: an <c>any</c> package is added to
    /// every supported architecture's database, against the same stored file. <c>repo-add</c>
    /// replaces an entry of the same name in each database it writes, so a replaced build only
    /// needs an explicit <c>repo-remove</c> from databases the new build does not go into — which
    /// happens when an architecture specific build replaces an <c>any</c> one.
    /// </para>
    /// <para>
    /// Any failure, of a tool or of the commit, discards the change tracker, so no row moved. The
    /// databases are then put back as they were: the new entry is removed from every database it
    /// reached, the file just written is deleted, and every replaced build is added back to each
    /// touched database it was listed in. That is why the replaced files are not deleted until
    /// after the commit. The file just written is never a replaced build's file: a publish moves the
    /// version forward, and a build for another architecture carries that architecture in its name.
    /// </para>
    /// <para>
    /// A compensating step that fails itself is logged at error and the original exception is
    /// allowed to surface; the repository database is then genuinely out of step with the rows, and
    /// the cure is reconciliation rather than anything this request can do.
    /// </para>
    /// </remarks>
    private async Task CommitWithSideEffectsAsync(
        PacmanRepository repository,
        PacmanPackage package,
        string uploadPath,
        IReadOnlyList<SupersededBuild> superseded,
        CancellationToken cancellationToken)
    {
        var destination = pathResolver.GetPackageFilePath(repository.Id, package.FileName);
        var targets = DatabaseArchitectures(repository, package.Architecture);
        var vacated = superseded
            .SelectMany(b => DatabaseArchitectures(repository, b.Architecture))
            .Distinct(StringComparer.Ordinal)
            .Where(a => !targets.Contains(a, StringComparer.Ordinal))
            .ToList();

        var added = new List<string>();
        var removed = new List<string>();

        try
        {
            fileSystem.CreateDirectory(pathResolver.GetRepositoryDirectory(repository.Id));
            fileSystem.Move(uploadPath, destination, overwrite: true);

            foreach (var architecture in targets)
            {
                fileSystem.CreateDirectory(DatabaseFor(repository, architecture).SyncDirectory);
                await cliRunner.RunToolCheckedAsync(
                    new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath, architecture, destination),
                    cancellationToken);
                added.Add(architecture);
            }

            foreach (var architecture in vacated)
            {
                await cliRunner.RunToolCheckedAsync(
                    new RepoRemove(repository.Id.ToString(), _pacmanConfig.DbPath, architecture, package.Name),
                    cancellationToken);
                removed.Add(architecture);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to publish {PackageName} to repository {RepositoryId}",
                package.Name, repository.Id);

            dbContext.ChangeTracker.Clear();

            try
            {
                if (added.Count > 0 || removed.Count > 0)
                {
                    await CompensatePublishAsync(repository, package, added, removed, destination, superseded);
                }
                else
                {
                    // The previous builds, if there were any, are untouched on disk under their own
                    // names and the rows still name them, so the repository is exactly as it was.
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
    /// Puts every database a failed publish touched back as it was: the new entry out, and every
    /// replaced build back in.
    /// </summary>
    /// <remarks>
    /// This takes no cancellation token on purpose. One of the ordinary ways the commit fails is
    /// the request being cancelled — the client disconnected while it was in flight — and the
    /// token the publish ran under is then already cancelled, so passing it on would abandon the
    /// cleanup at its first await and leave the database file advertising a package no row
    /// describes. Undoing a side effect is work that has to happen whatever became of the request
    /// that caused it.
    /// </remarks>
    /// <param name="repository">The repository published into.</param>
    /// <param name="package">The build that failed to publish.</param>
    /// <param name="added">The databases <c>repo-add</c> put the new build into.</param>
    /// <param name="removed">The databases a replaced build was taken out of.</param>
    /// <param name="destination">Where the new build's file was moved.</param>
    /// <param name="superseded">The builds the publish would have replaced.</param>
    private async Task CompensatePublishAsync(
        PacmanRepository repository,
        PacmanPackage package,
        IReadOnlyList<string> added,
        IReadOnlyList<string> removed,
        string destination,
        IReadOnlyList<SupersededBuild> superseded)
    {
        foreach (var architecture in added)
        {
            await cliRunner.RunToolCheckedAsync(
                new RepoRemove(repository.Id.ToString(), _pacmanConfig.DbPath, architecture, package.Name),
                CancellationToken.None);
        }

        // Nothing references the file that was just moved into place: no row was committed, and
        // every entry that named it has just been removed.
        DeleteQuietly(destination, "package file from a rolled back publish");

        // The replaced files have deliberately not been deleted yet, so putting their entries back
        // is enough to leave the databases describing what the rows still say.
        var touched = added.Concat(removed).ToList();
        foreach (var previous in superseded)
        {
            var previousPath = pathResolver.GetPackageFilePath(repository.Id, previous.FileName);
            if (!fileSystem.Exists(previousPath))
            {
                continue;
            }

            foreach (var architecture in DatabaseArchitectures(repository, previous.Architecture)
                         .Where(a => touched.Contains(a, StringComparer.Ordinal)))
            {
                await cliRunner.RunToolCheckedAsync(
                    new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath, architecture, previousPath),
                    CancellationToken.None);
            }
        }
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
        if (IsAny(architecture)
            || repository.SupportedArchitectures.Contains(architecture, StringComparer.Ordinal))
        {
            return;
        }

        throw new PackageArchitectureMismatchException(architecture, repository.SupportedArchitectures);
    }

    /// <summary>
    /// The architectures whose databases list a package built for <paramref name="packageArchitecture"/>:
    /// every architecture the repository supports for an <c>any</c> build, and otherwise its own
    /// architecture when the repository supports it.
    /// </summary>
    private static List<string> DatabaseArchitectures(PacmanRepository repository, string packageArchitecture) =>
        IsAny(packageArchitecture)
            ? repository.SupportedArchitectures.ToList()
            : repository.SupportedArchitectures
                .Where(a => string.Equals(a, packageArchitecture, StringComparison.Ordinal))
                .ToList();

    /// <summary>
    /// Whether <paramref name="architecture"/> is the architecture independent <c>any</c>.
    /// </summary>
    private static bool IsAny(string architecture) =>
        string.Equals(architecture, PackageArchitectureMismatchException.AnyArchitecture, StringComparison.Ordinal);

    /// <summary>
    /// The database <c>repo-add</c> maintains for one of a repository's architectures.
    /// </summary>
    private RepositoryDatabase DatabaseFor(PacmanRepository repository, string architecture) =>
        new(repository.Id.ToString(), _pacmanConfig.DbPath, architecture);

    /// <summary>
    /// Rejects an upload that does not move the package forward, which is what keeps a published
    /// version's bytes the bytes every client that synced this repository was promised.
    /// </summary>
    /// <remarks>
    /// Ordering is <see cref="AlpmVersion"/>'s rather than the string's, because it has to be the
    /// same ordering the pacman client reading this repository will apply — <c>1.10</c> is newer
    /// than <c>1.9</c>, and an epoch outranks everything to its right. Only the build for the same
    /// architecture is compared: a build for a different architecture is a different package rather
    /// than a re-push of the same one, and carries the version it was built with.
    /// </remarks>
    /// <param name="existing">The build of this package, for this architecture, already held.</param>
    /// <param name="version">The version being published.</param>
    /// <exception cref="PackageNotNewerException">
    /// The repository already holds this package, for this architecture, at that version or newer.
    /// </exception>
    private static void RequireForwardProgress(PacmanPackage existing, string version)
    {
        if (AlpmVersion.IsNewerThan(version, existing.Version))
        {
            return;
        }

        throw new PackageNotNewerException(existing.Name, existing.Version, version, existing.Architecture);
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

        // The same lookup the read path uses, so this pair matches at most one row for the reason
        // GetPackageByNameAsync gives.
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
        // Resolved before the row is detached, since the file name and architecture are read off
        // the row.
        var packageFilePath = pathResolver.GetPackageFilePath(repository.Id, package.FileName);
        var architectures = DatabaseArchitectures(repository, package.Architecture);

        await RemoveFromDatabasesAsync(repository, package, architectures, packageFilePath, cancellationToken);

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
                await CompensateCommittedRemoveAsync(repository, package, architectures, packageFilePath);
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
    /// Runs <c>repo-remove</c> against every database that lists the package. If one fails after
    /// others succeeded, the package is added back to those, so that a tool failure still leaves
    /// every database as it was and the request aborts with nothing written.
    /// </summary>
    private async Task RemoveFromDatabasesAsync(
        PacmanRepository repository,
        PacmanPackage package,
        IEnumerable<string> architectures,
        string packageFilePath,
        CancellationToken cancellationToken)
    {
        var removed = new List<string>();
        try
        {
            foreach (var architecture in architectures)
            {
                await cliRunner.RunToolCheckedAsync(
                    new RepoRemove(repository.Id.ToString(), _pacmanConfig.DbPath, architecture, package.Name),
                    cancellationToken);
                removed.Add(architecture);
            }
        }
        catch (Exception e) when (removed.Count > 0)
        {
            logger.LogError(e, "Failed to remove {PackageName} from every database of repository {RepositoryId}",
                package.Name, repository.Id);

            try
            {
                await CompensateCommittedRemoveAsync(repository, package, removed, packageFilePath);
            }
            catch (Exception compensation)
            {
                logger.LogError(compensation,
                    "Failed to undo the partial deletion of {PackageName} in repository {RepositoryId}. "
                    + "Its databases and its rows now disagree.",
                    package.Name, repository.Id);
            }

            throw;
        }
    }

    /// <summary>
    /// Undoes <c>repo-remove</c> runs that succeeded before a commit, or a later
    /// <c>repo-remove</c>, that did not, by adding the package file back to those databases.
    /// </summary>
    /// <remarks>
    /// This takes no cancellation token, for the reason
    /// <see cref="CompensatePublishAsync"/> does not: one of the ordinary ways the commit
    /// fails is the request being cancelled, and a compensation running on the cancelled token
    /// would abandon itself at its first await and leave the database file missing a package the
    /// rows still describe.
    /// </remarks>
    private async Task CompensateCommittedRemoveAsync(
        PacmanRepository repository,
        PacmanPackage package,
        IEnumerable<string> architectures,
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

        foreach (var architecture in architectures)
        {
            await cliRunner.RunToolCheckedAsync(
                new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath, architecture, packageFilePath),
                CancellationToken.None);
        }
    }
}
