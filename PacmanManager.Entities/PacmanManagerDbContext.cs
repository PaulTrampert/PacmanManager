using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

public class PacmanManagerDbContext : DbContext
{
    public DbSet<PacmanRepository> PacmanRepositories { get; set; }
    
    public DbSet<PacmanPackage> PacmanPackages { get; set; }
    
    public DbSet<User> Users { get; set; }
    
    public DbSet<ExternalProviderUserMapping> UserMappings { get; set; }
    
    public DbSet<PacmanAccessToken> PacmanAccessTokens { get; set; }

    /// <summary>
    /// Off-request work and the commands it runs, of every kind.
    /// </summary>
    public DbSet<Job> Jobs { get; set; }

    /// <summary>
    /// The entities each job acts on.
    /// </summary>
    public DbSet<JobAffectedEntity> JobAffectedEntities { get; set; }
    
    public PacmanManagerDbContext(DbContextOptions<PacmanManagerDbContext> options) : base(options)
    {
    }
}