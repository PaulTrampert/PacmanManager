namespace PacmanManager.Worker;

/// <summary>
/// How the Worker receives messages: the RabbitMQ broker it connects to and the queue it listens on.
/// </summary>
public class MessagingConfig
{
    /// <summary>
    /// The configuration section this is bound from.
    /// </summary>
    public const string Section = "Messaging";

    /// <summary>
    /// The name, under <c>ConnectionStrings</c>, of the RabbitMQ connection string, an
    /// <c>amqp://user:password@host:port</c> URI.
    /// </summary>
    public const string ConnectionStringName = "rabbitmq";

    /// <summary>
    /// The queue the Worker listens on when configuration names no other.
    /// </summary>
    public const string DefaultQueueName = "pacmanmanager.worker";

    /// <summary>
    /// The RabbitMQ queue the Worker receives its messages from. Wolverine declares it at startup if
    /// it does not exist.
    /// </summary>
    public string QueueName { get; set; } = DefaultQueueName;
}
