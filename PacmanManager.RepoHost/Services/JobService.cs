using Microsoft.EntityFrameworkCore;
using PacmanManager.Authentication;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using JobModel = PacmanManager.RepoHost.Models.Job;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IJobService"/> backed by the application database.
/// </summary>
/// <remarks>
/// Every job it returns has first been put to <see cref="JobAccessPolicy"/>, together with the
/// current actor; a job the policy hides is reported as missing.
/// </remarks>
/// <param name="dbContext">The database the jobs are stored in.</param>
/// <param name="actorAccessor">Supplies the actor whose view of the jobs every method returns.</param>
/// <param name="accessPolicy">Decides which jobs the actor may see.</param>
internal class JobService(
    PacmanManagerDbContext dbContext,
    IActorAccessor actorAccessor,
    JobAccessPolicy accessPolicy) : IJobService
{
    /// <inheritdoc />
    public async Task<JobModel?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var actor = await actorAccessor.GetActorAsync(ct);

        var job = await dbContext.Jobs
            .AsNoTracking()
            .Include(j => j.AffectedEntities)
            .SingleOrDefaultAsync(j => j.Id == jobId, ct);

        // The policy is asked even when there is no such job, so that an anonymous caller is
        // challenged rather than told the id names nothing.
        return accessPolicy.CheckRead(job, actor) switch
        {
            RepositoryAccess.Allowed => JobModel.FromJob(job!),
            RepositoryAccess.Unauthenticated => throw new NoCurrentUserException(),
            _ => null,
        };
    }
}
