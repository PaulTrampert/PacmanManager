using System.Linq.Expressions;
using PacmanManager.Authentication;
using PacmanManager.Entities;
using PacmanManager.Packages.Authorization;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The single definition of who may see a <see cref="Job"/>, one at a time or as a listing.
/// </summary>
/// <remarks>
/// <para>The rules are:</para>
/// <list type="bullet">
///   <item><description>A system actor may see every job.</description></item>
///   <item><description>An actor with no user is <see cref="RepositoryAccess.Unauthenticated"/>, whether or not the job exists.</description></item>
///   <item><description>A job is visible to the user who scheduled it. To everyone else it does not exist.</description></item>
/// </list>
/// <para>
/// A job the actor may not see is <see cref="RepositoryAccess.NotFound"/>, never
/// <see cref="RepositoryAccess.Forbidden"/>: it is reported exactly as a job that does not exist,
/// so its id confirms nothing.
/// </para>
/// <para>
/// <see cref="CheckRead"/> answers for one job and <see cref="VisibleTo"/> for a listing; the two
/// state the same rules, so a job is in an actor's listing exactly when the actor may read it.
/// </para>
/// <para>
/// No scope is consulted. The scope vocabulary in <see cref="ScopeValues"/> names no jobs entity, and
/// a job is only ever the caller's own record of work it was already permitted to schedule.
/// </para>
/// <para>
/// Like <see cref="RepositoryAccessPolicy"/>, it is intentionally free of any database, HTTP or
/// logging dependency, which makes every rule a plain unit test.
/// </para>
/// </remarks>
internal sealed class JobAccessPolicy
{
    /// <summary>
    /// Decides whether <paramref name="actor"/> may see <paramref name="job"/>.
    /// </summary>
    /// <param name="job">
    /// The job being read, or <c>null</c> when no job has the requested id. A missing job gets the
    /// verdict another user's job would, so the two cannot be told apart, and an anonymous caller is
    /// challenged either way.
    /// </param>
    /// <param name="actor">The actor attempting the read.</param>
    /// <returns>The outcome of the check.</returns>
    public RepositoryAccess CheckRead(Job? job, Actor actor)
    {
        if (actor.IsSystem)
        {
            return job is null ? RepositoryAccess.NotFound : RepositoryAccess.Allowed;
        }

        if (actor.User is not { } user)
        {
            return RepositoryAccess.Unauthenticated;
        }

        return job is not null && job.UserId == user.Id
            ? RepositoryAccess.Allowed
            : RepositoryAccess.NotFound;
    }

    /// <summary>
    /// Decides whether <paramref name="actor"/> may list jobs at all.
    /// </summary>
    /// <param name="actor">The actor attempting the listing.</param>
    /// <returns>
    /// <see cref="RepositoryAccess.Unauthenticated"/> for an actor with no user that is not the system;
    /// otherwise <see cref="RepositoryAccess.Allowed"/>, and <see cref="VisibleTo"/> says which jobs
    /// the listing holds.
    /// </returns>
    public RepositoryAccess CheckList(Actor actor) =>
        actor.IsSystem || actor.User is not null
            ? RepositoryAccess.Allowed
            : RepositoryAccess.Unauthenticated;

    /// <summary>
    /// Builds the predicate describing every job <paramref name="actor"/> may see: every job for a
    /// system actor, and for anyone else the jobs their user scheduled.
    /// </summary>
    /// <param name="actor">The actor to build visibility for.</param>
    /// <returns>An expression suitable for composition into an EF Core query.</returns>
    /// <remarks>
    /// An actor with no user sees nothing. <see cref="CheckList"/> challenges such an actor before a
    /// listing is ever queried, so that is only a second line of defence.
    /// </remarks>
    public Expression<Func<Job, bool>> VisibleTo(Actor actor)
    {
        if (actor.IsSystem)
        {
            return _ => true;
        }

        if (actor.User is not { } user)
        {
            return _ => false;
        }

        var userId = user.Id;
        return j => j.UserId == userId;
    }
}
