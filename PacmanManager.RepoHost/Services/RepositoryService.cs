using Microsoft.EntityFrameworkCore;
using Npgsql;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PTrampert.QueryObjects;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IRepositoryService"/> backed by the application database and the
/// pacman CLI tools.
/// </summary>
/// <remarks>
/// Authorization is enforced structurally rather than by remembering to check. Every read and
/// write starts from <see cref="VisibleAsync"/>, which is the only place in this class that
/// touches <see cref="PacmanManagerDbContext.PacmanRepositories"/> and which applies
/// <see cref="RepositoryAccessPolicy.VisibleTo"/> before returning. A method that forgets the
/// rules therefore has to name the <see cref="DbSet{TEntity}"/> to do so, which is both obvious
/// in review and caught by <c>RepositoryServiceEnforcementTests</c>.
/// </remarks>
internal class RepositoryService(
    PacmanManagerDbContext dbContext,
    ICliToolRunner cliRunner,
    IActorAccessor actorAccessor,
    RepositoryAccessPolicy accessPolicy,
    ILogger<RepositoryService> logger,
    IFileSystem fileSystem,
    IPackagePathResolver pathResolver,
    IServerUrlProvider serverUrls) : IRepositoryService
{
    /// <summary>
    /// Sets the per-response fields on a repository that has already been materialised.
    /// </summary>
    /// <remarks>
    /// <see cref="Repository.ServerUrl"/> is built here, after the query, and never in
    /// <see cref="Repository.Projection"/>: it is not a column.
    /// </remarks>
    private Repository? Complete(Repository? repository, string serverUrl) =>
        repository is null ? null : repository with { ServerUrl = serverUrl };

    /// <inheritdoc cref="Complete(Repository?, string)"/>
    private Repository? Complete(Repository? repository) =>
        repository is null ? null : Complete(repository, serverUrls.GetServerUrl());

    /// <summary>
    /// The database <c>repo-add</c> maintains for one of a repository's architectures.
    /// </summary>
    private RepositoryDatabase DatabaseFor(Guid id, string architecture) =>
        new(id.ToString(), pathResolver.GetRepositoryDirectory(id), architecture);

    /// <summary>
    /// Removes a repository's directory and everything in it: its package files and every file
    /// <c>repo-add</c> wrote for each of its architectures.
    /// </summary>
    private void DeleteRepositoryDirectory(Guid id)
    {
        var directory = pathResolver.GetRepositoryDirectory(id);
        if (fileSystem.DirectoryExists(directory))
        {
            fileSystem.DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// The repositories the current actor is allowed to see. Every other method in this class
    /// starts here.
    /// </summary>
    private async ValueTask<IQueryable<PacmanRepository>> VisibleAsync(CancellationToken cancellationToken)
    {
        var actor = await actorAccessor.GetActorAsync(cancellationToken);
        return dbContext.PacmanRepositories.Where(accessPolicy.VisibleTo(actor));
    }

    /// <summary>
    /// Loads a repository for a change, translating the outcome of <paramref name="verdict"/> into
    /// the result or exception the caller expects.
    /// </summary>
    /// <param name="id">The repository to load.</param>
    /// <param name="verdict">
    /// The <see cref="RepositoryAccessPolicy"/> verdict for the change being made, so that the
    /// operation, not this helper, decides which question is asked.
    /// </param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The tracked entity, or null when the actor must be told it does not exist.</returns>
    private async Task<PacmanRepository?> LoadForChangeAsync(
        Guid id,
        Func<PacmanRepository, Actor, RepositoryAccess> verdict,
        CancellationToken cancellationToken)
    {
        var visible = await VisibleAsync(cancellationToken);
        var repository = await visible
            .Include(r => r.Owner)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (repository is null)
        {
            // Either it genuinely does not exist, or it is private and belongs to someone else.
            // Both cases have to look identical from the outside.
            return null;
        }

        var actor = await actorAccessor.GetActorAsync(cancellationToken);
        switch (verdict(repository, actor))
        {
            case RepositoryAccess.Allowed:
                return repository;
            case RepositoryAccess.NotFound:
                return null;
            case RepositoryAccess.Unauthenticated:
                throw new NoCurrentUserException();
            default:
                throw new RepositoryForbiddenException(id);
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is the database refusing a row because another
    /// repository already holds its name, as opposed to any other failure to save.
    /// </summary>
    /// <param name="exception">The exception <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> threw.</param>
    /// <remarks>
    /// The unique index is the only thing that decides a collision; nothing checks for one before
    /// writing, so two concurrent writes of the same name cannot both succeed. The index is matched by
    /// the name the model gives it rather than a literal, so the check follows the index when its
    /// columns change.
    /// </remarks>
    private bool IsNameCollision(DbUpdateException exception)
    {
        var nameIndex = dbContext.Model
            .FindEntityType(typeof(PacmanRepository))!
            .GetIndexes()
            .Single(i => i.IsUnique)
            .GetDatabaseName();

        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
               && postgres.ConstraintName == nameIndex;
    }

    /// <summary>
    /// The exception a name collision is reported as, which the handler maps to <c>409</c>.
    /// </summary>
    /// <param name="collision">The database's refusal, kept as the inner exception.</param>
    private static ItemExistsException NameTaken(DbUpdateException collision) =>
        // The handler never copies the message into the response, but it is still worded to say
        // nothing about the repository that holds the name.
        new("A repository with this name already exists.", collision);

    /// <summary>
    /// <paramref name="repository"/> with <see cref="Repository.Permissions"/> set for
    /// <paramref name="actor"/>.
    /// </summary>
    /// <remarks>
    /// Asked of the materialised model, after the query has run, so the rule is never translated to
    /// SQL and costs no query. It is the same <see cref="RepositoryAccessPolicy.CheckOwnership"/>
    /// that <see cref="UpdateRepositoryAsync"/> and <see cref="DeleteRepositoryAsync"/> enforce
    /// with, through <see cref="RepositoryAccessPolicy.CheckUpdate"/> and
    /// <see cref="RepositoryAccessPolicy.CheckDelete"/>, so what a caller is told it may do and what
    /// it is allowed to do cannot disagree.
    /// </remarks>
    private Repository WithPermissions(Repository repository, Actor actor) => repository with
    {
        Permissions = new RepositoryPermissions
        {
            Update = Permits(repository, actor, ScopeValues.ActionNames.Update),
            Delete = Permits(repository, actor, ScopeValues.ActionNames.Delete),
        }
    };

    /// <summary>
    /// Whether <paramref name="actor"/> may make the change <paramref name="action"/> to
    /// <paramref name="repository"/>.
    /// </summary>
    private bool Permits(Repository repository, Actor actor, string action) =>
        accessPolicy.CheckOwnership(repository.Owner.Id, repository.IsPublic, actor, action)
        == RepositoryAccess.Allowed;

    /// <summary>
    /// <paramref name="repository"/> with <see cref="Repository.Permissions"/> set for the current
    /// actor, or null when there is no repository.
    /// </summary>
    private async Task<Repository?> WithPermissionsAsync(Repository? repository, CancellationToken cancellationToken) =>
        repository is null
            ? null
            : WithPermissions(repository, await actorAccessor.GetActorAsync(cancellationToken));

    public async Task<Repository?> GetRepositoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var visible = await VisibleAsync(cancellationToken);
        var repository = await visible
            .Where(r => r.Id == id)
            .Select(Repository.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return Complete(await WithPermissionsAsync(repository, cancellationToken));
    }

    public async Task<Repository?> GetRepositoryByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var visible = await VisibleAsync(cancellationToken);

        // The name alone is the database's unique index over repositories, so this matches at most
        // one row and needs no tie-break.
        var repository = await visible
            .Where(r => r.Name == name)
            .Select(Repository.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return Complete(await WithPermissionsAsync(repository, cancellationToken));
    }

    public async Task<Stream?> GetRepositoryFileByIdAsync(
        Guid id,
        string architecture,
        CancellationToken cancellationToken = default) =>
        OpenDatabase(await GetRepositoryByIdAsync(id, cancellationToken), architecture);

    public async Task<Stream?> GetRepositoryFileByNameAsync(
        string name,
        string architecture,
        CancellationToken cancellationToken = default) =>
        OpenDatabase(await GetRepositoryByNameAsync(name, cancellationToken), architecture);

    public async Task<RepositoryFile?> GetRepositoryDatabaseByIdAsync(
        Guid id,
        string architecture,
        RepositoryDatabaseKind kind,
        CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryByIdAsync(id, cancellationToken);
        if (repository is null || !repository.SupportedArchitectures.Contains(architecture, StringComparer.Ordinal))
            return null;

        return fileSystem.OpenRepositoryFile(DatabaseFor(repository.Id, architecture).FilePathOf(kind));
    }

    /// <summary>
    /// Opens a visible repository's database for one of its architectures, or nothing when the
    /// repository is absent or does not support the architecture.
    /// </summary>
    private Stream? OpenDatabase(Repository? repository, string architecture)
    {
        if (repository is null || !repository.SupportedArchitectures.Contains(architecture, StringComparer.Ordinal))
            return null;

        return fileSystem.OpenRead(DatabaseFor(repository.Id, architecture).FilePath);
    }

    public async Task<Repository> CreateRepositoryAsync(WriteRepositoryRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var actor = await actorAccessor.GetActorAsync(cancellationToken);

        switch (accessPolicy.CheckCreate(actor))
        {
            case RepositoryAccess.Allowed:
                break;
            case RepositoryAccess.Unauthenticated:
                throw new NoCurrentUserException();
            default:
                throw new InsufficientScopeException(
                    ScopeValues.EntityNames.Repositories, ScopeValues.ActionNames.Create);
        }

        var owner = actor.User!;
        var repository = new PacmanRepository
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            SupportedArchitectures = request.SupportedArchitectures.Distinct(StringComparer.Ordinal).ToList(),
            IsPublic = request.IsPublic,
            CreatedAt = now,
            UpdatedAt = now,
            OwnerId = owner.Id,
            Owner = owner
        };

        try
        {
            await dbContext.AddAsync(repository, cancellationToken);

            // repo-add creates the empty databases that make the repository real, one per supported
            // architecture, so a failure here has to fail the request. Checked, because the runner
            // reports a tool that ran and failed only through its exit code: an unchecked call would
            // commit a row naming a database file that was never written.
            foreach (var architecture in repository.SupportedArchitectures)
            {
                var database = DatabaseFor(repository.Id, architecture);
                fileSystem.CreateDirectory(database.DatabaseDirectory);
                await cliRunner.RunToolCheckedAsync(new RepoAdd(database), cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            // The directory is named for the new repository's id, never its name, so this removes
            // only what this call wrote, even when the failure is a collision with an existing
            // repository.
            DeleteRepositoryDirectory(repository.Id);

            if (e is DbUpdateException dbUpdate && IsNameCollision(dbUpdate))
            {
                throw NameTaken(dbUpdate);
            }

            logger.LogError(e, "Failed to create repository: {@Request}", request);
            throw;
        }

        return Complete(WithPermissions(Repository.FromPacmanRepository(repository), actor))!;
    }

    public async Task<Repository?> UpdateRepositoryAsync(Guid id, WriteRepositoryRequest update, CancellationToken cancellationToken = default)
    {
        var repository = await LoadForChangeAsync(id, accessPolicy.CheckUpdate, cancellationToken);
        if (repository is null)
        {
            return null;
        }

        repository.Name = update.Name;
        repository.IsPublic = update.IsPublic;
        repository.SupportedArchitectures = update.SupportedArchitectures.Distinct(StringComparer.Ordinal).ToList();
        repository.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsNameCollision(e))
        {
            throw NameTaken(e);
        }

        return Complete(await WithPermissionsAsync(Repository.FromPacmanRepository(repository), cancellationToken))!;
    }

    public async Task<bool> DeleteRepositoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repository = await LoadForChangeAsync(id, accessPolicy.CheckDelete, cancellationToken);
        if (repository is null)
        {
            return false;
        }

        dbContext.Remove(repository);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The row is the source of truth. Everything the repository owns on disk -- its package
        // files and every architecture's databases -- is under one directory, so removing that
        // leaves nothing to enumerate or forget. Files left behind are inert once nothing points
        // at them, so failing to remove them is worth a warning but not worth failing the delete.
        try
        {
            DeleteRepositoryDirectory(id);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Deleted repository {RepositoryId} but could not remove {RepositoryDirectory}",
                id, pathResolver.GetRepositoryDirectory(id));
        }

        return true;
    }

    public async Task<PaginatedResponse<Repository>> GetRepositoriesAsync(
        PaginationParams paginationParams,
        RepositoryFilter? filter = null,
        SortOptions<RepositorySortField>? sort = null,
        CancellationToken cancellationToken = default)
    {
        // Visibility comes first and the caller's criteria are ANDed onto it, so a filter can only
        // ever remove rows from the visible set.
        var query = (await VisibleAsync(cancellationToken))
            .Where(filter ?? new RepositoryFilter());

        var sortOptions = sort ?? new SortOptions<RepositorySortField>();
        var total = await query.CountAsync(cancellationToken);
        var results = await query
            .ApplySort(sortOptions)
            .Skip(paginationParams.Offset)
            .Take(paginationParams.PageSize)
            .Select(Repository.Projection)
            .ToListAsync(cancellationToken);

        var actor = await actorAccessor.GetActorAsync(cancellationToken);
        var serverUrl = serverUrls.GetServerUrl();
        return new PaginatedResponse<Repository>
        {
            Results = results.Select(r => Complete(WithPermissions(r, actor), serverUrl)!).ToList(),
            Offset = paginationParams.Offset,
            Total = total,
            Sort = sortOptions.Resolve()
        };
    }
}
