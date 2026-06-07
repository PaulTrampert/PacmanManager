using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Startup.LibAlpm;

namespace PacmanManager.RepoHost.Services;

internal class RepositoryService(
    PacmanManagerDbContext dbContext, 
    ICliToolRunner cliRunner, 
    IOptionsSnapshot<PacmanConfigSettings> pacmanSettings, 
    ILogger<RepositoryService> logger,
    IFileSystem fileSystem) : IRepositoryService
{
    private PacmanConfigSettings _pacmanConfig = pacmanSettings.Value;

    private string GetRepositoryFileName(string repositoryName)
    {
        return Path.Combine(_pacmanConfig.DbPath, "sync", $"{repositoryName}.db.tar.gz");
    }

    public async Task<Repository?> GetRepositoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repository = await dbContext.PacmanRepositories.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return repository is not null ? Repository.FromPacmanRepository(repository) : null;
    }

    public async Task<Repository?> GetRepositoryByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var repository = await dbContext.PacmanRepositories.SingleOrDefaultAsync(r => r.Name == name, cancellationToken);
        return repository is not null ? Repository.FromPacmanRepository(repository) : null;
    }

    public async Task<Stream?> GetRepositoryFileByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryByNameAsync(name, cancellationToken);
        if (repository is null)
            return null;
        
        var repoFileName = GetRepositoryFileName(repository.Name);
        return fileSystem.OpenRead(repoFileName);
    }
    
    public async Task<Repository> CreateRepositoryAsync(WriteRepositoryRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var repository = new PacmanRepository
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Architecture = request.Architecture,
            CreatedAt = now,
            UpdatedAt = now,
        };
        
        try
        {
            await dbContext.PacmanRepositories.AddAsync(repository, cancellationToken);

            await cliRunner.RunToolAsync(new RepoAdd(repository.Name, _pacmanConfig.DbPath), cancellationToken);
            
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create repository: {@Request}", request);
            var expectedRepoPath = GetRepositoryFileName(repository.Name);
            if (fileSystem.Exists(expectedRepoPath))
            {
                fileSystem.Delete(expectedRepoPath);
            }
            throw;
        }

        return Repository.FromPacmanRepository(repository);
    }

    public async Task<PaginatedResponse<Repository>> GetRepositoriesAsync(PaginationParams paginationParams, CancellationToken cancellationToken = default)
    {
        var query = dbContext.PacmanRepositories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
        {
            query = query.Where(r => r.Name.Contains(paginationParams.SearchTerm));
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(paginationParams.Offset)
            .Take(paginationParams.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<Repository>
        {
            Results = entities.Select(Repository.FromPacmanRepository),
            Offset = paginationParams.Offset,
            Total = total
        };
    }
}