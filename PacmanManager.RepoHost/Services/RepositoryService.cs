using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Startup.LibAlpm;

namespace PacmanManager.RepoHost.Services;

public class RepositoryService(
    PacmanManagerDbContext dbContext, 
    ICliToolRunner cliRunner, 
    IOptionsSnapshot<PacmanConfigSettings> pacmanSettings, 
    ILogger<RepositoryService> logger)
{
    private PacmanConfigSettings _pacmanConfig = pacmanSettings.Value;

    private string GetRepositoryFileName(string repositoryName)
    {
        return Path.Combine(_pacmanConfig.DbPath, "sync", $"{repositoryName}.db.tar.gz");
    }

    public async Task<Repository?> GetRepositoryByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        var repository = await dbContext.PacmanRepositories.FindAsync([repositoryId], cancellationToken: cancellationToken);
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
        return File.OpenRead(repoFileName);
    }
    
    public async Task<Repository> CreateRepositoryAsync(CreateRepositoryRequest request, CancellationToken cancellationToken = default)
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
            if (File.Exists(expectedRepoPath))
            {
                File.Delete(expectedRepoPath);
            }
            throw;
        }

        return Repository.FromPacmanRepository(repository);
    }
}