namespace PacmanManager.Entities;

/// <summary>
/// Thrown by <see cref="Job.As{TMessage}"/> when the job's <see cref="Job.MessageType"/> does not name
/// the type it was asked to read the message as: a handler was given the wrong kind of job.
/// </summary>
/// <param name="jobId">The job whose message was read.</param>
/// <param name="storedType">The message type the job records.</param>
/// <param name="requestedType">The message type the caller asked for.</param>
public class JobMessageTypeException(Guid jobId, string storedType, string requestedType)
    : InvalidOperationException($"Job {jobId} holds a {storedType} message, not a {requestedType}.")
{
    /// <summary>
    /// The job whose message was read.
    /// </summary>
    public Guid JobId { get; } = jobId;

    /// <summary>
    /// The message type the job records.
    /// </summary>
    public string StoredType { get; } = storedType;

    /// <summary>
    /// The message type the caller asked for.
    /// </summary>
    public string RequestedType { get; } = requestedType;
}
