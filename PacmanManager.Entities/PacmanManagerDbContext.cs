using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

public class PacmanManagerDbContext : DbContext
{
    public DbSet<PacmanRepository> PacmanRepositories { get; set; }
    
    public DbSet<PacmanPackage> PacmanPackages { get; set; }
    
    public PacmanManagerDbContext(DbContextOptions<PacmanManagerDbContext> options) : base(options)
    {
    }
}