using LibAlpmSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.Packages.CliTools;
using PacmanManager.Packages.Exceptions;
using PacmanManager.Packages.Infrastructure;
using PacmanManager.Packages.LibAlpm;

namespace PacmanManager.Packages;

/// <summary>
/// The default <see cref="IPackagePublisher"/>, backed by the application database, the package
/// file tree and the pacman CLI tools.
/// </summary>
/// <remarks>
/// Every change runs its side effects first and commits last, so that a failed tool costs nothing
/// more than a discarded change tracker, and undoes the side effects it made when the commit fails.
/// </remarks>
/// <param name="dbContext">The context the rows are staged and committed in.</param>
/// <param name="cliRunner">Runs <c>repo-add</c> and <c>repo-remove</c>.</param>
/// <param name="fileSystem">Where package files are moved and deleted.</param>
/// <param name="pathResolver">Where a repository's files and databases live, and what a package file is named.</param>
/// <param name="databaseLock">Serializes changes to one repository's databases.</param>
/// <param name="packageReader">Reads an upload's metadata through libalpm.</param>
/// <param name="logger">Logs failures and the compensation that follows them.</param>
public class PackagePublisher(
    PacmanManagerDbContext dbContext,
    ICliToolRunner cliRunner,
    IFileSystem fileSystem,
    IPackagePathResolver pathResolver,
    IRepositoryDatabaseLock databaseLock,
    IPackageFileReader packageReader,
    ILogger<PackagePublisher> logger) : IPackagePublisher
{
    /// <inheritdoc />
    public async Task<PublishedPackage> PublishAsync(
        PacmanRepository repository,
        User publisher,
        PackageUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(upload);

        var uploadPath = upload.Path;

        // Steps 1 and 2, authorizing the change and receiving the upload, are the caller's.
        // Step 3: everything stored about the package comes out of the file itself, so a file
        // libalpm cannot open is a rejected upload rather than a server error.
        using var loaded = packageReader.Read(uploadPath);
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
        // The caller has already authorized the change, so every package in the repository is one
        // it may replace.
        var sameName = await dbContext.PacmanPackages
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

        return new PublishedPackage(package, created);
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
                fileSystem.CreateDirectory(DatabaseFor(repository, architecture).DatabaseDirectory);
                await cliRunner.RunToolCheckedAsync(
                    new RepoAdd(DatabaseFor(repository, architecture), destination),
                    cancellationToken);
                added.Add(architecture);
            }

            foreach (var architecture in vacated)
            {
                await cliRunner.RunToolCheckedAsync(
                    new RepoRemove(DatabaseFor(repository, architecture), package.Name),
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
                new RepoRemove(DatabaseFor(repository, architecture), package.Name),
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
                    new RepoAdd(DatabaseFor(repository, architecture), previousPath),
                    CancellationToken.None);
            }
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
        string.Equals(architecture, Architectures.Any, StringComparison.Ordinal);

    /// <summary>
    /// The database <c>repo-add</c> maintains for one of a repository's architectures.
    /// </summary>
    private RepositoryDatabase DatabaseFor(PacmanRepository repository, string architecture) =>
        new(repository.Id.ToString(), pathResolver.GetRepositoryDirectory(repository.Id), architecture);

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
        PackageUpload upload,
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

    /// <inheritdoc />
    public async Task RemoveAsync(
        PacmanRepository repository,
        PacmanPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(package);

        // Publish and delete take the same lock: repo-remove rewrites the file repo-add writes, and
        // it is held across the side effect *and* the commit so no other writer sees the window in
        // which the database file and the rows disagree.
        using var _ = await databaseLock.AcquireAsync(repository.Id, cancellationToken);

        await RemoveWithSideEffectsAsync(repository, package, cancellationToken);
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
                    new RepoRemove(DatabaseFor(repository, architecture), package.Name),
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
                new RepoAdd(DatabaseFor(repository, architecture), packageFilePath),
                CancellationToken.None);
        }
    }
}
