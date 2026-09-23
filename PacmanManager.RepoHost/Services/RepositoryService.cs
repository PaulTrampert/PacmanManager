using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
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
    IOptionsSnapshot<PacmanConfigSettings> pacmanSettings,
    ILogger<RepositoryService> logger,
    IFileSystem fileSystem) : IRepositoryService
{
    private PacmanConfigSettings _pacmanConfig = pacmanSettings.Value;

    private string GetRepositoryFileName(Guid id)
    {
        return Path.Combine(_pacmanConfig.DbPath, "sync", $"{id}.db.tar.gz");
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

    public async Task<Repository?> GetRepositoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var visible = await VisibleAsync(cancellationToken);
        return await visible
            .Where(r => r.Id == id)
            .Select(Repository.Projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Repository?> GetRepositoryByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var visible = await VisibleAsync(cancellationToken);

        // The name alone is the database's unique index over repositories, so this matches at most
        // one row and needs no tie-break.
        return await visible
            .Where(r => r.Name == name)
            .Select(Repository.Projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Stream?> GetRepositoryFileByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryByIdAsync(id, cancellationToken);
        if (repository is null)
            return null;

        var repoFileName = GetRepositoryFileName(repository.Id);
        return fileSystem.OpenRead(repoFileName);
    }

    public async Task<Stream?> GetRepositoryFileByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryByNameAsync(name, cancellationToken);
        if (repository is null)
            return null;

        var repoFileName = GetRepositoryFileName(repository.Id);
        return fileSystem.OpenRead(repoFileName);
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
            Architecture = request.Architecture,
            IsPublic = request.IsPublic,
            CreatedAt = now,
            UpdatedAt = now,
            OwnerId = owner.Id,
            Owner = owner
        };

        try
        {
            await dbContext.AddAsync(repository, cancellationToken);

            // repo-add creates the empty database that makes the repository real, so a failure here
            // has to fail the request. Checked, because the runner reports a tool that ran and
            // failed only through its exit code: an unchecked call would commit a row naming a
            // database file that was never written.
            await cliRunner.RunToolCheckedAsync(
                new RepoAdd(repository.Id.ToString(), _pacmanConfig.DbPath),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            // The file is named for the new repository's id, never its name, so this removes only
            // what this call wrote, even when the failure is a collision with an existing repository.
            var expectedRepoPath = GetRepositoryFileName(repository.Id);
            if (fileSystem.Exists(expectedRepoPath))
            {
                fileSystem.Delete(expectedRepoPath);
            }

            if (e is DbUpdateException dbUpdate && IsNameCollision(dbUpdate))
            {
                throw NameTaken(dbUpdate);
            }

            logger.LogError(e, "Failed to create repository: {@Request}", request);
            throw;
        }

        return Repository.FromPacmanRepository(repository);
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
        repository.Architecture = update.Architecture;
        repository.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsNameCollision(e))
        {
            throw NameTaken(e);
        }

        return Repository.FromPacmanRepository(repository);
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

        // The row is the source of truth. A file left behind is inert once nothing points at it,
        // so failing to remove it is worth a warning but not worth failing the delete.
        var repoFileName = GetRepositoryFileName(id);
        try
        {
            if (fileSystem.Exists(repoFileName))
            {
                fileSystem.Delete(repoFileName);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Deleted repository {RepositoryId} but could not remove {RepositoryFile}", id, repoFileName);
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

        var total = await query.CountAsync(cancellationToken);
        var results = await query
            .ApplySort(sort ?? new SortOptions<RepositorySortField>())
            .Skip(paginationParams.Offset)
            .Take(paginationParams.PageSize)
            .Select(Repository.Projection)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<Repository>
        {
            Results = results,
            Offset = paginationParams.Offset,
            Total = total
        };
    }
}
