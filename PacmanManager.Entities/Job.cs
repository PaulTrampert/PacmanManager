using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

/// <summary>
/// One unit of off-request work, and the command it runs. One entity and one table hold every kind of
/// job: the command is stored as JSON beside the name of its type, and <see cref="As{TMessage}"/> reads
/// it back as that type.
/// </summary>
/// <remarks>
/// The job is composed with its message rather than being a base type commands derive from, so a
/// command stays a plain data object and this project never references the commands. The job, not the
/// command, carries the user and the tracking state.
/// </remarks>
public record Job
{
    /// <summary>
    /// The options a message is written to and read from <see cref="Message"/> with.
    /// </summary>
    private static readonly JsonSerializerOptions MessageSerializerOptions = JsonSerializerOptions.Web;

    /// <summary>
    /// Internal database Id for the job.
    /// </summary>
    [Key]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// When the job was scheduled, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Where the job is in its life. <see cref="JobState.Completed"/> and <see cref="JobState.Failed"/>
    /// are final.
    /// </summary>
    public JobState State { get; set; } = JobState.Pending;

    /// <summary>
    /// Why the job failed. Set when <see cref="State"/> is <see cref="JobState.Failed"/>.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Id of the user who scheduled the job, or <c>null</c> when the <c>System</c> actor, which has no
    /// user, scheduled it. It decides who may see and delete the job, and is the user the job runs as.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// The user who scheduled the job, when <see cref="UserId"/> is set and the user was loaded.
    /// </summary>
    public virtual User? User { get; set; }

    /// <summary>
    /// Names the type of the command <see cref="Message"/> holds: its
    /// <see cref="MessageTypeOf{TMessage}">full type name</see>.
    /// </summary>
    [Required]
    [MaxLength(JobValidationConstants.MessageTypeMaxLength)]
    public required string MessageType { get; init; }

    /// <summary>
    /// The command itself, as JSON. Stored as <c>jsonb</c> under Npgsql.
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public required string Message { get; init; }

    /// <summary>
    /// The entities the job acts on, as far as they are known.
    /// </summary>
    public virtual ICollection<JobAffectedEntity> AffectedEntities { get; set; } = [];

    /// <summary>
    /// The value <see cref="MessageType"/> holds for a message of type <typeparamref name="TMessage"/>.
    /// </summary>
    /// <typeparam name="TMessage">The command type.</typeparam>
    /// <returns>The type's full name.</returns>
    public static string MessageTypeOf<TMessage>() => typeof(TMessage).FullName!;

    /// <summary>
    /// A new <see cref="JobState.Pending"/> job holding <paramref name="message"/>, with
    /// <see cref="MessageType"/> naming <typeparamref name="TMessage"/>.
    /// </summary>
    /// <param name="message">The command the job runs.</param>
    /// <param name="userId">The user scheduling the job, or <c>null</c> for the <c>System</c> actor.</param>
    /// <typeparam name="TMessage">The command type.</typeparam>
    /// <returns>The job, not yet added to any context.</returns>
    public static Job For<TMessage>(TMessage message, Guid? userId) => new()
    {
        UserId = userId,
        MessageType = MessageTypeOf<TMessage>(),
        Message = JsonSerializer.Serialize(message, MessageSerializerOptions),
    };

    /// <summary>
    /// A typed view of this job, carrying its id, its user and its message read as
    /// <typeparamref name="TMessage"/>.
    /// </summary>
    /// <typeparam name="TMessage">The command type the job is expected to hold.</typeparam>
    /// <returns>The typed view. It is not tracked or mapped by EF.</returns>
    /// <exception cref="JobMessageTypeException">
    /// <see cref="MessageType"/> does not name <typeparamref name="TMessage"/>.
    /// </exception>
    public Job<TMessage> As<TMessage>()
    {
        var requestedType = MessageTypeOf<TMessage>();
        if (MessageType != requestedType)
        {
            throw new JobMessageTypeException(Id, MessageType, requestedType);
        }

        var message = JsonSerializer.Deserialize<TMessage>(Message, MessageSerializerOptions)!;
        return new Job<TMessage>(Id, UserId, User, message);
    }
}

/// <summary>
/// A typed view of a <see cref="Job"/>, made by <see cref="Job.As{TMessage}"/>. It is not mapped by EF;
/// changes to the job's state are made on the <see cref="Job"/> itself.
/// </summary>
/// <param name="Id">The job's id.</param>
/// <param name="UserId">Id of the user who scheduled the job, or <c>null</c> for the <c>System</c> actor.</param>
/// <param name="User">The user who scheduled the job, when it was loaded with the job.</param>
/// <param name="Message">The job's message, read as <typeparamref name="TMessage"/>.</param>
/// <typeparam name="TMessage">The command type the job holds.</typeparam>
public record Job<TMessage>(Guid Id, Guid? UserId, User? User, TMessage Message);
