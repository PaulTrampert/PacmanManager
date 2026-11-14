namespace PacmanManager.Entities;

/// <summary>
/// Where a <see cref="Job"/> is in its life. <see cref="Completed"/> and <see cref="Failed"/> are
/// final: a job in either state never changes state again.
/// </summary>
public enum JobState
{
    /// <summary>
    /// Scheduled and waiting to run, or waiting to be retried after a failure a retry may get past.
    /// </summary>
    Pending,

    /// <summary>
    /// Being run by a handler.
    /// </summary>
    Running,

    /// <summary>
    /// Ran to completion. Final.
    /// </summary>
    Completed,

    /// <summary>
    /// Rejected, or failed with no retry remaining; <see cref="Job.Error"/> says why. Final.
    /// </summary>
    Failed,
}
